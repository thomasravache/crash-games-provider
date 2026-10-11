using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.Wallet;
using Apogeu.Betting.Infrastructure.Messaging;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Apogeu.Betting.Features.CashOut;

/// <summary>
/// Handles <see cref="CreditCashOut"/> (ADR-0007, SPEC-0023): while the credit of a cash-out is not confirmed, it is sent again
/// with the same deterministic id <c>bet:{betId}:credit</c> and, unconfirmed, scheduled again durably with a growing delay —
/// on any instance, also after the one that cashed out died. Once the wallet confirms it, the player gets <c>BalanceUpdated</c>.
/// A permanent refusal of the wallet propagates (reconciliation).
/// </summary>
public static class CreditCashOutHandler
{
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

    public static async Task HandleAsync(
        CreditCashOut command,
        IBetStore store,
        IOperatorWallet wallet,
        BetsNotifier notifier,
        BettingMetrics metrics,
        IMessageBus bus,
        ILogger<CreditCashOut> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(bus);

        if (await store.FindByIdAsync(command.BetId, ct).ConfigureAwait(false) is not { CashOut: { Credited: false } } found)
        {
            // Confirmed already (the credit tried at once, or an earlier attempt).
            return;
        }

        var player = new PlayerRef(new OperatorId(command.OperatorId), new PlayerId(command.PlayerId));
        var credit = CashOutSettlement.CreditOf(command.BetId, player, new Currency(command.Currency), new RoundId(command.RoundId), command.Payout);
        WalletResult? result;
        try
        {
            result = BetTransactions.Original(await wallet.CreditAsync(credit, ct).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // ADR-0007: any failure leaves the credit unconfirmed, so it is scheduled again.
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            BettingLog.CreditPending(logger, command.BetId, exception.GetType().Name);
            result = null;
        }

        switch (result)
        {
            case WalletOk ok:
                var balance = ok.BalanceAfter.Amount.Value;
                if (await store.ConfirmCreditAsync(command.BetId, balance, ct).ConfigureAwait(false))
                {
                    metrics.CreditPending(-1);
                    BettingLog.CreditConfirmed(logger, command.BetId);
                    await NotifyAsync(notifier, found, new BalanceUpdatedMessage(new Money(balance), command.Currency), logger).ConfigureAwait(false);
                }

                return;
            case WalletUnavailable { Retryable: false }:
                BettingLog.CreditRefused(logger, command.BetId);
                throw new InvalidOperationException($"The wallet refused the credit of the cash-out of bet {command.BetId} permanently.");
            default:
                BettingLog.CreditPending(logger, command.BetId, result?.GetType().Name ?? "no answer");
                var delay = Backoff[Math.Min(command.Attempt, Backoff.Length - 1)];
                await bus.ScheduleAsync(command with { Attempt = command.Attempt + 1 }, delay).ConfigureAwait(false);
                return;
        }
    }

    private static async Task NotifyAsync(BetsNotifier notifier, AcceptedBet found, BalanceUpdatedMessage message, ILogger logger)
    {
        try
        {
            await notifier.BalanceUpdatedAsync(found.Bet.Owner, message).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The credit is confirmed; only the message to the player failed.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BettingLog.CashOutNotSent(logger, found.Bet.Id, exception);
        }
    }
}
