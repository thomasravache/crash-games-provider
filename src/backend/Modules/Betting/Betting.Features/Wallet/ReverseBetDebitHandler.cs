using Apogeu.Betting.Infrastructure.Messaging;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Apogeu.Betting.Features.Wallet;

/// <summary>
/// Handles <see cref="ReverseBetDebit"/> (ADR-0007): the reversal is done once the wallet answers it applied, repeated, or
/// without a debit to reverse (<see cref="WalletTransactionNotFound"/> is final, logged for reconciliation); otherwise it is
/// scheduled again, durably, with a growing delay until the wallet confirms it. The rollback id is deterministic, so any
/// number of attempts reverses the debit once.
/// </summary>
public static class ReverseBetDebitHandler
{
    private static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];

    public static async Task HandleAsync(
        ReverseBetDebit command,
        IOperatorWallet wallet,
        IMessageBus bus,
        ILogger<ReverseBetDebit> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(bus);

        var rollback = new WalletRollback(
            BetTransactions.Rollback(command.BetId),
            BetTransactions.Debit(command.BetId),
            new PlayerRef(new OperatorId(command.OperatorId), new PlayerId(command.PlayerId)),
            new Currency(command.Currency),
            command.RoundId is { } round ? new RoundId(round) : null,
            command.BetId);

        WalletResult? result;
        try
        {
            result = BetTransactions.Original(await wallet.RollbackAsync(rollback, ct).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // ADR-0007: any failure leaves the reversal unconfirmed, so it is scheduled again.
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            BettingLog.RefundPending(logger, command.BetId, exception.GetType().Name);
            result = null;
        }

        switch (result)
        {
            case WalletOk:
                BettingLog.DebitReversed(logger, command.BetId, nameof(WalletOk));
                return;
            case WalletTransactionNotFound:
                BettingLog.ReconciliationNoDebit(logger, command.BetId);
                return;
            default:
                var delay = Backoff[Math.Min(command.Attempt, Backoff.Length - 1)];
                await bus.ScheduleAsync(command with { Attempt = command.Attempt + 1 }, delay).ConfigureAwait(false);
                return;
        }
    }
}
