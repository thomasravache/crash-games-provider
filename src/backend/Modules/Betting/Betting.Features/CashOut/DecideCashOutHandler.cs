using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
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
        ArgumentNullException.ThrowIfNull(bus);

        if (await store.FindByIdAsync(command.BetId, ct).ConfigureAwait(false) is not
            { Bet: { Status: BetStatus.Active, RoundId: { } roundId } bet, CashOutRequestedAt: { } receivedAt } found)
        {
            return;
        }

        CashOutDecision decision;
        try
        {
            decision = await arbiter.DecideAsync(found.SalaId, roundId, receivedAt, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The leader could not be reached: the decision is scheduled again with the same instant.
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            BettingLog.CashOutPending(logger, bet.Id, exception.GetType().Name);
            var delay = Backoff[Math.Min(command.Attempt, Backoff.Length - 1)];
            await bus.ScheduleAsync(command with { Attempt = command.Attempt + 1 }, delay).ConfigureAwait(false);
            return;
        }

        if (decision is CashOutAccepted accepted)
        {
            if (await settlement.SettleAsync(found, accepted.Multiplier.Value, automatic: false).ConfigureAwait(false) is { } cashedOut)
            {
                await SendAsync(() => notifier.CashedOutAsync(bet.Owner, cashedOut), bet.Id, logger).ConfigureAwait(false);
            }

            return;
        }

        metrics.TooLate();
        await store.ClearCashOutRequestAsync(bet.Id, ct).ConfigureAwait(false);
        await SendAsync(() => notifier.CashOutRejectedAsync(bet.Owner, new CashOutRejected(bet.Id, BettingErrorCodes.CashOutTooLate)), bet.Id, logger)
            .ConfigureAwait(false);
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
