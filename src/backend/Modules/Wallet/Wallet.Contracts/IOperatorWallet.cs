using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Wallet.Contracts;

/// <summary>
/// Port of the operator wallet (seamless wallet, ADR-0007; SPEC-0021 §6, contract v2). Every operation is idempotent by
/// <see cref="TransactionId"/>: repeating one returns <see cref="WalletDuplicate"/> with the original result. The adapter is
/// chosen per operator (<c>demo</c> now; <c>http</c> and aggregators later).
/// </summary>
public interface IOperatorWallet
{
    /// <summary>Debits a bet: <see cref="WalletOk"/>, <see cref="WalletInsufficientFunds"/>, <see cref="WalletDuplicate"/> or <see cref="WalletUnavailable"/>.</summary>
    Task<WalletResult> DebitAsync(WalletOperation op, CancellationToken ct);

    /// <summary>Credits a win: <see cref="WalletOk"/>, <see cref="WalletDuplicate"/> or <see cref="WalletUnavailable"/>.</summary>
    Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct);

    /// <summary>
    /// Reverses an earlier debit by its transaction id: <see cref="WalletOk"/>, <see cref="WalletDuplicate"/>,
    /// <see cref="WalletTransactionNotFound"/> (no such debit; nothing posted) or <see cref="WalletUnavailable"/>.
    /// </summary>
    Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct);

    /// <summary>The current balance of <paramref name="player"/> in <paramref name="currency"/>.</summary>
    Task<Balance> GetBalanceAsync(PlayerRef player, Currency currency, CancellationToken ct);
}
