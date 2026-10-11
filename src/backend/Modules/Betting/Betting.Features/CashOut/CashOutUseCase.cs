using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.Sessions;
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
    BettingMetrics metrics,
    TimeProvider time,
    ILogger<CashOutUseCase> logger)
{
    /// <summary>How long the answer waits for the leader's decision before it is <c>CashOutPending</c> (SPEC-0023 §4).</summary>
    public static readonly TimeSpan DecisionDeadline = TimeSpan.FromMilliseconds(200);

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

        var bet = found.Bet;
        if (bet.Status == BetStatus.Lost)
        {
            // Its round crashed while it was active: a cash-out now is too late (SPEC-0024).
            metrics.TooLate();
            return Refuse(BettingErrorCodes.CashOutTooLate);
        }

        if (bet is not { Status: BetStatus.Active, RoundId: { } roundId })
        {
            return Refuse(BettingErrorCodes.BetNotActive);
        }

        if (found.CashOutRequestedAt is not null)
        {
            return Pending(bet.Id);
        }

        switch (await DecideAsync(found.SalaId, roundId, bet.Id, receivedAt).ConfigureAwait(false))
        {
            case CashOutAccepted accepted:
                return await settlement.SettleAsync(found, accepted.Multiplier.Value, automatic: false).ConfigureAwait(false) is { } settled
                    ? Answer(settled)
                    : Refuse(BettingErrorCodes.BetNotActive);
            case CashOutTooLate:
                metrics.TooLate();
                return Refuse(BettingErrorCodes.CashOutTooLate);
            default:
                // Not decided in time, or undecided (the leader is taking over the round): decided later, once.
                return await RequestAsync(bet, receivedAt).ConfigureAwait(false);
        }
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

    /// <summary>Records the request for a later decision and answers it pending; what the bet became, when it changed meanwhile.</summary>
    private async Task<BettingOutcome<CashOutResult>> RequestAsync(Bet bet, DateTimeOffset receivedAt)
    {
        if (await store.RequestCashOutAsync(bet.Id, receivedAt, CancellationToken.None).ConfigureAwait(false))
        {
            return Pending(bet.Id);
        }

        var current = await store.FindByIdAsync(bet.Id, CancellationToken.None).ConfigureAwait(false);
        return current switch
        {
            not null when CashOutSettlement.Answer(current) is { } cashedOut => Answer(cashedOut),
            { CashOutRequestedAt: not null } => Pending(bet.Id),
            _ => Refuse(BettingErrorCodes.BetNotActive),
        };
    }

    private static BettingOutcome<CashOutResult> Answer(CashedOut cashedOut) => new(new CashOutResult(cashedOut, Pending: null), null);

    private static BettingOutcome<CashOutResult> Pending(Guid betId) => new(new CashOutResult(CashedOut: null, new CashOutPending(betId)), null);

    private BettingOutcome<CashOutResult> Refuse(string code)
    {
        metrics.RejectedWith(code);
        return new BettingOutcome<CashOutResult>(null, code);
    }
}
