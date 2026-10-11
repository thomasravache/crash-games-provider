using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.Wallet;
using Apogeu.Betting.Infrastructure.Messaging;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Apogeu.Betting.Features.Settlement;

/// <summary>
/// Handles <see cref="RefundBet"/> (ADR-0007, SPEC-0024): the refund of a bet of an annulled round, on its own queue. While the
/// wallet does not confirm it, the same rollback <c>bet:{betId}:rollback</c> is sent again, scheduled durably with a growing
/// delay — on any instance, also after the one that settled the round died. Its first attempt tells the player
/// <c>BetRefunded</c> (refunded with the balance, or pending); a refund confirmed later sends <c>BalanceUpdated</c>. A permanent
/// refusal of the wallet propagates (reconciliation).
/// </summary>
public static class RefundBetHandler
{
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

    public static async Task HandleAsync(
        RefundBet command,
        IBetStore store,
        IOperatorWallet wallet,
        BetsNotifier notifier,
        BettingMetrics metrics,
        IMessageBus bus,
        ILogger<RefundBet> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(bus);

        if (await store.FindByIdAsync(command.BetId, ct).ConfigureAwait(false) is not { Refund.Confirmed: false } found)
        {
            // Confirmed already (an earlier attempt).
            return;
        }

        var notice = new Notice(notifier, found, command, logger);
        switch (await RollbackAsync(wallet, command, logger, ct).ConfigureAwait(false))
        {
            case WalletOk ok:
                await ConfirmAsync(store, metrics, notice, command.Amount, ok.BalanceAfter.Amount.Value, ct).ConfigureAwait(false);
                return;
            case WalletTransactionNotFound:
                // Reconciliation (SPEC-0022 rule): the wallet has no debit of this bet to reverse, so nothing was refunded.
                BettingLog.ReconciliationNoDebit(logger, command.BetId);
                var balance = await wallet.GetBalanceAsync(Player(command), new Currency(command.Currency), ct).ConfigureAwait(false);
                await ConfirmAsync(store, metrics, notice, 0.00m, balance.Amount.Value, ct).ConfigureAwait(false);
                return;
            case WalletUnavailable { Retryable: false }:
                BettingLog.RoundRefundRefused(logger, command.BetId);
                await notice.PendingAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"The wallet refused the refund of bet {command.BetId} permanently.");
            case var result:
                BettingLog.RoundRefundPending(logger, command.BetId, result?.GetType().Name ?? "no answer");
                await notice.PendingAsync().ConfigureAwait(false);
                var delay = Backoff[Math.Min(command.Attempt, Backoff.Length - 1)];
                await bus.ScheduleAsync(command with { Attempt = command.Attempt + 1, Announce = false }, delay).ConfigureAwait(false);
                return;
        }
    }

    private static PlayerRef Player(RefundBet command) => new(new OperatorId(command.OperatorId), new PlayerId(command.PlayerId));

    /// <summary>The rollback, unwrapped from a duplicate; <see langword="null"/> when the call failed (its outcome is unknown).</summary>
    private static async Task<WalletResult?> RollbackAsync(IOperatorWallet wallet, RefundBet command, ILogger logger, CancellationToken ct)
    {
        var rollback = new WalletRollback(
            BetTransactions.Rollback(command.BetId),
            BetTransactions.Debit(command.BetId),
            Player(command),
            new Currency(command.Currency),
            new RoundId(command.RoundId),
            command.BetId);
        try
        {
            return BetTransactions.Original(await wallet.RollbackAsync(rollback, ct).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // ADR-0007: any failure leaves the refund unconfirmed, so it is scheduled again.
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            BettingLog.RoundRefundPending(logger, command.BetId, exception.GetType().Name);
            return null;
        }
    }

    private static async Task ConfirmAsync(IBetStore store, BettingMetrics metrics, Notice notice, decimal refunded, decimal balance, CancellationToken ct)
    {
        var confirmed = await store.ConfirmRefundAsync(notice.Command.BetId, refunded, balance, ct).ConfigureAwait(false);
        if (confirmed)
        {
            metrics.RefundPending(-1);
        }

        await notice.ConfirmedAsync(refunded, balance, confirmed).ConfigureAwait(false);
    }

    /// <summary>What the player is told about the refund of <see cref="Command"/>.</summary>
    private sealed record Notice(BetsNotifier Notifier, AcceptedBet Found, RefundBet Command, ILogger Logger)
    {
        /// <summary>Unconfirmed: <c>BetRefunded</c> pending without a balance, on the first attempt only.</summary>
        public Task PendingAsync() =>
            Command.Announce ? SendAsync(() => Notifier.BetRefundedAsync(Found.Bet.Owner, Refunded(Command.Amount, balance: null))) : Task.CompletedTask;

        /// <summary>Confirmed: <c>BetRefunded</c> with the balance on the first attempt, <c>BalanceUpdated</c> on a later one.</summary>
        public Task ConfirmedAsync(decimal refunded, decimal balance, bool recorded)
        {
            if (Command.Announce)
            {
                return SendAsync(() => Notifier.BetRefundedAsync(Found.Bet.Owner, Refunded(refunded, balance)));
            }

            return recorded
                ? SendAsync(() => Notifier.BalanceUpdatedAsync(Found.Bet.Owner, new BalanceUpdatedMessage(new Money(balance), Command.Currency)))
                : Task.CompletedTask;
        }

        private BetRefunded Refunded(decimal refunded, decimal? balance) =>
            new(
                Command.BetId,
                Command.RoundId,
                new Money(refunded),
                balance is { } after ? new Money(after) : null,
                balance is null ? RefundStatus.Pending : RefundStatus.Refunded);

        private async Task SendAsync(Func<Task> send)
        {
            try
            {
                await send().ConfigureAwait(false);
            }
#pragma warning disable CA1031 // The refund stands; only the message to the player failed.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                BettingLog.SettlementNotSent(Logger, Command.BetId, exception);
            }
        }
    }
}
