using Apogeu.Wallet.Contracts;

namespace Apogeu.Betting.Features.Wallet;

/// <summary>The deterministic wallet transaction ids of a bet (SPEC-0021 §6): <c>bet:{betId}:debit|rollback</c>.</summary>
internal static class BetTransactions
{
    public static TransactionId Debit(Guid betId) => new($"bet:{betId:D}:debit");

    public static TransactionId Rollback(Guid betId) => new($"bet:{betId:D}:rollback");

    /// <summary>The result of the first attempt, when the wallet answers a repeated transaction id.</summary>
    public static WalletResult Original(WalletResult result) =>
        result is WalletDuplicate duplicate ? Original(duplicate.OriginalResult) : result;
}
