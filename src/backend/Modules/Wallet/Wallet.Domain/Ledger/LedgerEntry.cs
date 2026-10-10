using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;

namespace Apogeu.Wallet.Domain.Ledger;

/// <summary>
/// An insert-only ledger entry (SPEC-0021 §2, ADR-0007), unique by <see cref="TransactionId"/>: its postings sum to zero.
/// <see cref="BalanceAfter"/> is the player's balance right after it, the result a repeated transaction gets back.
/// </summary>
/// <param name="TransactionId">Idempotency key of the operation.</param>
/// <param name="Kind">What the entry records.</param>
/// <param name="Player">Player of the entry.</param>
/// <param name="Currency">Currency of every posting.</param>
/// <param name="RoundId">Round of the bet (none for a recharge).</param>
/// <param name="BetId">Bet (none for a recharge).</param>
/// <param name="OriginalTransactionId">For a rollback, the debit it reverses.</param>
/// <param name="Postings">The postings, summing to zero.</param>
/// <param name="BalanceAfter">Balance of the player right after the entry.</param>
/// <param name="RecordedAt">When it was recorded.</param>
public sealed record LedgerEntry(
    TransactionId TransactionId,
    LedgerEntryKind Kind,
    PlayerRef Player,
    Currency Currency,
    RoundId? RoundId,
    Guid? BetId,
    TransactionId? OriginalTransactionId,
    IReadOnlyList<LedgerPosting> Postings,
    Money BalanceAfter,
    DateTimeOffset RecordedAt);
