using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Wallet.Contracts;

/// <summary>Reversal of an earlier debit, identified by its transaction id (SPEC-0021 §6, contract v2).</summary>
/// <param name="TransactionId">Deterministic idempotency key of the reversal (<c>bet:{betId}:rollback</c>).</param>
/// <param name="OriginalTransactionId">Transaction id of the debit being reversed.</param>
/// <param name="Player">Player whose debit is reversed.</param>
/// <param name="Currency">Currency of the account.</param>
/// <param name="RoundId">Round of the bet.</param>
/// <param name="BetId">Bet the reversal belongs to.</param>
public sealed record WalletRollback(
    TransactionId TransactionId,
    TransactionId OriginalTransactionId,
    PlayerRef Player,
    Currency Currency,
    RoundId RoundId,
    Guid BetId);
