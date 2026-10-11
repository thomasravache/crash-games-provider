using System.Globalization;

namespace Apogeu.Wallet.Contracts;

/// <summary>
/// Idempotency key of a wallet operation (SPEC-0021 §6, contract v2), deterministic per bet operation:
/// <c>bet:{betId}:debit</c>, <c>bet:{betId}:credit</c> or <c>bet:{betId}:rollback</c>, with the bet id in the Guid format
/// <c>"D"</c>. Repeating a key returns the original result.
/// </summary>
public readonly record struct TransactionId(string Value)
{
    /// <summary><c>bet:{betId}:debit</c>.</summary>
    public static TransactionId ForDebit(Guid betId) => ForBet(betId, "debit");

    /// <summary><c>bet:{betId}:credit</c>.</summary>
    public static TransactionId ForCredit(Guid betId) => ForBet(betId, "credit");

    /// <summary><c>bet:{betId}:rollback</c>.</summary>
    public static TransactionId ForRollback(Guid betId) => ForBet(betId, "rollback");

    public override string ToString() => Value;

    private static TransactionId ForBet(Guid betId, string operation) =>
        new(string.Create(CultureInfo.InvariantCulture, $"bet:{betId:D}:{operation}"));
}
