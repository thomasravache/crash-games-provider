using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.Sessions;
using Apogeu.Betting.Features.Settlement;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.Rounds.Contracts;
using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features.CashOut;

/// <summary>
/// <c>CashOut</c> (SPEC-0023 §6, contract v2): the leader of the room decides with the server instant the request was
/// received (latency tolerance 0). Decided within <see cref="DecisionDeadline"/>, the answer is <c>CashedOut</c> (or
/// <c>CASHOUT_TOO_LATE</c>); otherwise the request is recorded with that instant and answered <c>CashOutPending</c>, and the
/// leader decides it later (<c>CashedOut</c> or <c>CashOutRejected</c> to the player). Repeating the cash-out of a bet
/// already cashed out answers the same <c>CashedOut</c>.
/// </summary>
public sealed class CashOutUseCase(
    IBetStore store,
    ICashOutArbiter arbiter,
    CashOutSettlement settlement,
    RoundSettler settler,
    BettingMetrics metrics,
    TimeProvider time,
    ILogger<CashOutUseCase> logger)
{
    /// <summary>How long the answer waits for the leader's decision before it is <c>CashOutPending</c> (SPEC-0023 §4).</summary>
    public static readonly TimeSpan DecisionDeadline = TimeSpan.FromMilliseconds(200);

    /// <summary>When the durable decision stored with the request runs, as a fallback of the one asked by the hub.</summary>
    public static readonly TimeSpan FallbackDecision = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// The cash-out received at <paramref name="receivedAt"/>. Not cancellable: once received, a closed connection does not lose
    /// the request (review G4, m2) — it is decided, or recorded as pending, all the same.
    /// </summary>
    public async Task<BettingOutcome<CashOutResult>> CashOutAsync(BettingSession session, CashOutRequest request, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        using var activity = BettingMetrics.Activities.StartActivity("betting.cash_out")?.SetTag("betting.bet_id", request.BetId);

        if (await store.FindAsync(session.Owner, request.BetId, CancellationToken.None).ConfigureAwait(false) is not { } found)
        {
            return Refuse(BettingErrorCodes.BetNotFound);
        }

        if (CashOutSettlement.Answer(found) is { } cashedOut)
        {
            return Answer(cashedOut);
        }

        if (found.Bet is { Status: BetStatus.Active } && found.CashOutRequestedAt is not null)
        {
            return Pending(found.Bet.Id);
        }

        if (found.Bet.Status == BetStatus.Active)
        {
            // M1 (SPEC-0024 review G4): the request is recorded before the leader is asked, so the crash never marks lost a bet
            // whose cash-out is being decided; its durable decision is a fallback of the one asked here.
            if (!await store.RequestCashOutAsync(found.Bet.Id, receivedAt, FallbackDecision, CancellationToken.None).ConfigureAwait(false))
            {
                return await ChangedAsync(found.Bet.Id, receivedAt).ConfigureAwait(false);
            }
        }

        return await DecideAndSettleAsync(found, receivedAt).ConfigureAwait(false);
    }

    /// <summary>The bet changed before its request was recorded: what it became (lost: still decided by the leader).</summary>
    private async Task<BettingOutcome<CashOutResult>> ChangedAsync(Guid betId, DateTimeOffset receivedAt)
    {
        var current = await store.FindByIdAsync(betId, CancellationToken.None).ConfigureAwait(false);
        return current switch
        {
            not null when CashOutSettlement.Answer(current) is { } cashedOut => Answer(cashedOut),
            { Bet.Status: BetStatus.Active, CashOutRequestedAt: not null } => Pending(betId),
            { Bet.Status: BetStatus.Lost } lost => await DecideAndSettleAsync(lost, receivedAt).ConfigureAwait(false),
            _ => Refuse(BettingErrorCodes.BetNotActive),
        };
    }

    /// <summary>
    /// Asks the leader and settles its decision. An active bet has its request recorded; a lost one (marked before the request
    /// could be recorded) is cashed out only if the leader accepts it, while its round's summary is open (M1).
    /// </summary>
    private async Task<BettingOutcome<CashOutResult>> DecideAndSettleAsync(AcceptedBet found, DateTimeOffset receivedAt)
    {
        var bet = found.Bet;
        if (bet is not { Status: BetStatus.Active or BetStatus.Lost, RoundId: { } roundId })
        {
            return Refuse(BettingErrorCodes.BetNotActive);
        }

        switch (await DecideAsync(found.SalaId, roundId, bet.Id, receivedAt).ConfigureAwait(false))
        {
            case CashOutAccepted accepted:
                var (settled, _) = await settlement.SettleOnceAsync(found, accepted.Multiplier.Value, automatic: false).ConfigureAwait(false);
                await settler.CloseAsync(roundId, CancellationToken.None).ConfigureAwait(false);
                return settled is not null ? Answer(settled) : TooLate();
            case CashOutTooLate:
                if (bet.Status == BetStatus.Active && await store.ClearCashOutRequestAsync(bet.Id, CancellationToken.None).ConfigureAwait(false))
                {
                    // Refused: the bet is lost like the others once the crash is settled.
                    await settler.SettleRefusedAsync(roundId, CancellationToken.None).ConfigureAwait(false);
                }

                return TooLate();
            default:
                if (bet.Status == BetStatus.Lost)
                {
                    // Nothing recorded to decide later: the leader did not answer for a bet already lost.
                    BettingLog.LateCashOutUnreconciled(logger, bet.Id, "the leader did not decide in time");
                    return TooLate();
                }

                // Not decided in time, or undecided (the leader is taking over the round): decided later, once.
                await store.ScheduleDecisionAsync(bet.Id, CancellationToken.None).ConfigureAwait(false);
                return Pending(bet.Id);
        }
    }

    private BettingOutcome<CashOutResult> TooLate()
    {
        metrics.TooLate();
        return Refuse(BettingErrorCodes.CashOutTooLate);
    }

    /// <summary>
    /// The leader's decision within <see cref="DecisionDeadline"/>; <see langword="null"/> when it did not come in time (and
    /// <see cref="CashOutUndecided"/> when the leader cannot decide yet).
    /// </summary>
    private async Task<CashOutDecision?> DecideAsync(string salaId, RoundId roundId, Guid betId, DateTimeOffset receivedAt)
    {
        var started = time.GetTimestamp();

        // Not tied to the caller: a decision already asked for is not abandoned by a closed connection.
        var deciding = arbiter.DecideAsync(salaId, roundId, receivedAt, CancellationToken.None);
        try
        {
            var decision = await deciding.WaitAsync(DecisionDeadline, time, CancellationToken.None).ConfigureAwait(false);
            metrics.Decided(time.GetElapsedTime(started));
            return decision;
        }
        catch (TimeoutException)
        {
            BettingLog.CashOutPending(logger, betId, "deadline");
        }
#pragma warning disable CA1031 // Any failure to reach the leader leaves the request to be decided later (CashOutPending).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BettingLog.CashOutPending(logger, betId, exception.GetType().Name);
        }

        // The late decision is not used: the recorded request is decided again by the leader with the same instant.
        _ = deciding.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        return null;
    }

    private static BettingOutcome<CashOutResult> Answer(CashedOut cashedOut) => new(new CashOutResult(cashedOut, Pending: null), null);

    private static BettingOutcome<CashOutResult> Pending(Guid betId) => new(new CashOutResult(CashedOut: null, new CashOutPending(betId)), null);

    private BettingOutcome<CashOutResult> Refuse(string code)
    {
        metrics.RejectedWith(code);
        return new BettingOutcome<CashOutResult>(null, code);
    }
}
