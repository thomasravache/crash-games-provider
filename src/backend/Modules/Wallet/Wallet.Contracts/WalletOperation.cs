using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Wallet.Contracts;

/// <summary>A debit (bet) or a credit (win) of the wallet (SPEC-0021 §6, contract v2).</summary>
/// <param name="TransactionId">Deterministic idempotency key (<c>bet:{betId}:debit|credit</c>).</param>
/// <param name="Player">Player whose balance changes.</param>
/// <param name="Currency">Currency of the account.</param>
/// <param name="Amount">Amount, always greater than zero.</param>
/// <param name="RoundId">Round of the bet.</param>
/// <param name="BetId">Bet the operation belongs to.</param>
public sealed record WalletOperation(
    TransactionId TransactionId,
    PlayerRef Player,
    Currency Currency,
    Money Amount,
    RoundId RoundId,
    Guid BetId);
