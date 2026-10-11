using Apogeu.Wallet.Contracts;

namespace Apogeu.Betting.Tests.Support;

/// <summary>One call Betting made to the wallet double, with the result it got.</summary>
/// <param name="Kind"><c>debit</c>, <c>credit</c> or <c>rollback</c>.</param>
/// <param name="TransactionId">Idempotency key of the call.</param>
/// <param name="Player">Player of the call.</param>
/// <param name="BetId">Bet of the call.</param>
/// <param name="Amount">Amount of a debit or credit; <see langword="null"/> for a rollback.</param>
/// <param name="OriginalTransactionId">The debit a rollback reverses.</param>
/// <param name="Result">What the double returned.</param>
internal sealed record WalletCall(
    string Kind,
    TransactionId TransactionId,
    PlayerRef Player,
    Guid BetId,
    decimal? Amount,
    TransactionId? OriginalTransactionId,
    WalletResult Result)
{
    /// <summary>Whether the call changed the balance (a first <see cref="WalletOk"/>, not a duplicate).</summary>
    public bool Applied => Result is WalletOk;
}
