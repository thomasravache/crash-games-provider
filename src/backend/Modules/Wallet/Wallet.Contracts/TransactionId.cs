namespace Apogeu.Wallet.Contracts;

#pragma warning disable MA0025 // Scaffold of SPEC-0021 (no logic yet): implemented in the green phase.

/// <summary>
/// Idempotency key of a wallet operation (SPEC-0021 §6, contract v2), deterministic per bet operation:
/// <c>bet:{betId}:debit</c>, <c>bet:{betId}:credit</c> or <c>bet:{betId}:rollback</c>. Repeating a key returns the original result.
/// </summary>
public readonly record struct TransactionId(string Value)
{
    /// <summary><c>bet:{betId}:debit</c>.</summary>
    public static TransactionId ForDebit(Guid betId) => throw new NotImplementedException();

    /// <summary><c>bet:{betId}:credit</c>.</summary>
    public static TransactionId ForCredit(Guid betId) => throw new NotImplementedException();

    /// <summary><c>bet:{betId}:rollback</c>.</summary>
    public static TransactionId ForRollback(Guid betId) => throw new NotImplementedException();

    public override string ToString() => Value;
}
#pragma warning restore MA0025
