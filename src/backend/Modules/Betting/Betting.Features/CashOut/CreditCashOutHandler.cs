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
/// The credit of a cash-out stored by the round events (<see cref="CreditCashOut.Announce"/>) runs at once and its first attempt
/// tells the player the cash-out itself: <c>CashedOut</c> credited with the balance, or pending. A permanent refusal of the
/// wallet propagates (reconciliation).
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

        var result = await CreditAsync(wallet, command, logger, ct).ConfigureAwait(false);
        switch (result)
        {
            case WalletOk ok:
                await ConfirmAsync(store, notifier, metrics, found, command, ok.BalanceAfter.Amount.Value, logger, ct).ConfigureAwait(false);
                return;
            case WalletUnavailable { Retryable: false }:
                BettingLog.CreditRefused(logger, command.BetId);
                if (command.Announce)
                {
                    await AnnounceAsync(notifier, found, balance: null, logger).ConfigureAwait(false);
                }

                throw new InvalidOperationException($"The wallet refused the credit of the cash-out of bet {command.BetId} permanently.");
            default:
                BettingLog.CreditPending(logger, command.BetId, result?.GetType().Name ?? "no answer");
                if (command.Announce)
                {
                    // Told once, as pending; BalanceUpdated follows when the credit is confirmed.
                    await AnnounceAsync(notifier, found, balance: null, logger).ConfigureAwait(false);
                }

                var delay = Backoff[Math.Min(command.Attempt, Backoff.Length - 1)];
                await bus.ScheduleAsync(command with { Attempt = command.Attempt + 1, Announce = false }, delay).ConfigureAwait(false);
                return;
        }
    }

    /// <summary>The credit, unwrapped from a duplicate; <see langword="null"/> when the call failed (its outcome is unknown).</summary>
    private static async Task<WalletResult?> CreditAsync(IOperatorWallet wallet, CreditCashOut command, ILogger logger, CancellationToken ct)
    {
        var player = new PlayerRef(new OperatorId(command.OperatorId), new PlayerId(command.PlayerId));
        var credit = CashOutSettlement.CreditOf(command.BetId, player, new Currency(command.Currency), new RoundId(command.RoundId), command.Payout);
        try
        {
            return BetTransactions.Original(await wallet.CreditAsync(credit, ct).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // ADR-0007: any failure leaves the credit unconfirmed, so it is scheduled again.
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            BettingLog.CreditPending(logger, command.BetId, exception.GetType().Name);
            return null;
        }
    }

    /// <summary>Records the confirmed credit and tells the player: the cash-out itself when announced, else the new balance.</summary>
    private static async Task ConfirmAsync(
        IBetStore store,
        BetsNotifier notifier,
        BettingMetrics metrics,
        AcceptedBet found,
        CreditCashOut command,
        decimal balance,
        ILogger logger,
        CancellationToken ct)
    {
        var confirmed = await store.ConfirmCreditAsync(command.BetId, balance, ct).ConfigureAwait(false);
        if (confirmed)
        {
            metrics.CreditPending(-1);
            BettingLog.CreditConfirmed(logger, command.BetId);
        }

        if (command.Announce)
        {
            await AnnounceAsync(notifier, found, balance, logger).ConfigureAwait(false);
        }
        else if (confirmed)
        {
            await NotifyAsync(() => notifier.BalanceUpdatedAsync(found.Bet.Owner, new BalanceUpdatedMessage(new Money(balance), command.Currency)), found.Bet.Id, logger)
                .ConfigureAwait(false);
        }
    }

    /// <summary><c>CashedOut</c> of a cash-out stored by the round events: credited with <paramref name="balance"/>, or pending.</summary>
    private static Task AnnounceAsync(BetsNotifier notifier, AcceptedBet found, decimal? balance, ILogger logger)
    {
        var cashOut = found.CashOut! with { Credited = balance is not null, Balance = balance };
        var cashedOut = CashOutSettlement.Answer(found with { CashOut = cashOut })!;
        return NotifyAsync(() => notifier.CashedOutAsync(found.Bet.Owner, cashedOut), found.Bet.Id, logger);
    }

    private static async Task NotifyAsync(Func<Task> send, Guid betId, ILogger logger)
    {
        try
        {
            await send().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The credit is confirmed; only the message to the player failed.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BettingLog.CashOutNotSent(logger, betId, exception);
        }
    }
}
