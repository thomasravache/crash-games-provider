using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features.Wallet;

/// <summary>
/// Handles <see cref="ReverseBetDebit"/>: the reversal is done once the wallet answers it applied, repeated or without a
/// debit to reverse (<see cref="WalletTransactionNotFound"/> is final); otherwise it throws so Wolverine tries again.
/// </summary>
public static class ReverseBetDebitHandler
{
    public static async Task HandleAsync(ReverseBetDebit command, IOperatorWallet wallet, ILogger<ReverseBetDebit> logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(wallet);

        var rollback = new WalletRollback(
            BetTransactions.Rollback(command.BetId),
            BetTransactions.Debit(command.BetId),
            new PlayerRef(new OperatorId(command.OperatorId), new PlayerId(command.PlayerId)),
            new Currency(command.Currency),
            command.RoundId is { } round ? new RoundId(round) : null,
            command.BetId);

        var result = BetTransactions.Original(await wallet.RollbackAsync(rollback, ct).ConfigureAwait(false));
        if (result is WalletOk or WalletTransactionNotFound)
        {
            BettingLog.DebitReversed(logger, command.BetId, result.GetType().Name);
            return;
        }

        throw new WalletReversalPendingException($"The wallet did not confirm the reversal of bet {command.BetId} ({result.GetType().Name}).");
    }
}
