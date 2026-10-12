using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.Settlement;
using Apogeu.Betting.Infrastructure.Messaging;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.Rounds.Contracts;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Apogeu.Betting.Features.CashOut;

/// <summary>
/// Handles <see cref="DecideCashOut"/> (SPEC-0023 §4): a manual cash-out answered <c>CashOutPending</c> is decided by the leader
/// with the reception instant recorded on the bet — retried while the leader cannot be reached — and the player gets
/// <c>CashedOut</c> or <c>CashOutRejected</c> (<c>CASHOUT_TOO_LATE</c>).
/// </summary>
public static class DecideCashOutHandler
{
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    public static async Task HandleAsync(
        DecideCashOut command,
        IBetStore store,
        ICashOutArbiter arbiter,
        CashOutSettlement settlement,
        BetsNotifier notifier,
        BettingMetrics metrics,
        RoundSettler settler,
        IMessageBus bus,
        ILogger<DecideCashOut> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(arbiter);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(settler);
        ArgumentNullException.ThrowIfNull(bus);

        if (await store.FindByIdAsync(command.BetId, ct).ConfigureAwait(false) is not
            { Bet: { Status: BetStatus.Active, RoundId: { } roundId } bet, CashOutRequestedAt: { } receivedAt } found)
        {
            return;
        }

        var decision = await DecideAsync(arbiter, found.SalaId, roundId, bet.Id, receivedAt, logger, ct).ConfigureAwait(false);

        // Undecided (the leader is taking the round over) is never too late: decided again later (review G4, M2). An earlier
        // concurrent reception recorded meanwhile is the one decided (m1).
        if (decision is not (CashOutAccepted or CashOutTooLate)
            || await store.FindByIdAsync(bet.Id, ct).ConfigureAwait(false) is not { CashOutRequestedAt: { } recorded } current
            || recorded != receivedAt)
        {
            if (decision is CashOutUndecided)
            {
                BettingLog.CashOutPending(logger, bet.Id, nameof(CashOutUndecided));
            }

            var delay = Backoff[Math.Min(command.Attempt, Backoff.Length - 1)];
            await bus.ScheduleAsync(command with { Attempt = command.Attempt + 1 }, delay).ConfigureAwait(false);
            return;
        }

        found = current;
        if (decision is CashOutAccepted accepted)
        {
            // Told only by the call that settled it: the hub's own decision and this fallback may both run (SPEC-0024 M1).
            if (await settlement.SettleOnceAsync(found, accepted.Multiplier.Value, automatic: false).ConfigureAwait(false) is { Settled: true, CashedOut: { } cashedOut })
            {
                await SendAsync(() => notifier.CashedOutAsync(bet.Owner, cashedOut), bet.Id, logger).ConfigureAwait(false);
            }

            // The last open bet of an ended round closes its summary (SPEC-0024).
            await settler.CloseAsync(roundId, ct).ConfigureAwait(false);
            return;
        }

        metrics.TooLate();
        await RefuseAsync(store, notifier, settler, bet, logger, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The request came at or after the crash: it is forgotten and the player told <c>CashOutRejected</c>; once the crash is
    /// settled the bet is lost like the others, and the summary may close (SPEC-0024).
    /// </summary>
    private static async Task RefuseAsync(IBetStore store, BetsNotifier notifier, RoundSettler settler, Bet bet, ILogger logger, CancellationToken ct)
    {
        if (!await store.ClearCashOutRequestAsync(bet.Id, ct).ConfigureAwait(false))
        {
            // Refused already by another decision of the same request.
            return;
        }

        await SendAsync(() => notifier.CashOutRejectedAsync(bet.Owner, new CashOutRejected(bet.Id, BettingErrorCodes.CashOutTooLate)), bet.Id, logger)
            .ConfigureAwait(false);
        await settler.SettleRefusedAsync(bet.RoundId!.Value, ct).ConfigureAwait(false);
    }

    /// <summary>The leader's decision; <see langword="null"/> when it could not be reached (decided again later, same instant).</summary>
    private static async Task<CashOutDecision?> DecideAsync(
        ICashOutArbiter arbiter,
        string salaId,
        RoundId roundId,
        Guid betId,
        DateTimeOffset receivedAt,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            return await arbiter.DecideAsync(salaId, roundId, receivedAt, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The leader could not be reached: the decision is scheduled again with the same instant.
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            BettingLog.CashOutPending(logger, betId, exception.GetType().Name);
            return null;
        }
    }

    private static async Task SendAsync(Func<Task> send, Guid betId, ILogger logger)
    {
        try
        {
            await send().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The decision is settled; only the message to the player failed (a new CashOut answers it).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BettingLog.CashOutNotSent(logger, betId, exception);
        }
    }
}
