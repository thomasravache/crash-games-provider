namespace Apogeu.Betting.Infrastructure.Messaging;

/// <summary>
/// Durable command (Wolverine, ADR-0002) to reverse the debit of a bet with <c>RollbackAsync</c> and the deterministic id
/// <c>bet:{betId}:rollback</c> until the wallet confirms it (ADR-0007). It is stored in the same transaction as the change
/// that requires it: a cancellation, or the voiding of a reservation whose bet was never confirmed.
/// </summary>
/// <param name="BetId">The bet whose debit <c>bet:{betId}:debit</c> is reversed.</param>
/// <param name="OperatorId">Operator of the player.</param>
/// <param name="PlayerId">Pseudonymous player id.</param>
/// <param name="Currency">Currency of the account.</param>
/// <param name="RoundId">Round of the bet; <see langword="null"/> for a bet queued for the next round (SPEC-0021@4).</param>
/// <param name="Attempt">How many times the wallet did not confirm it yet (backoff of the next attempt).</param>
public sealed record ReverseBetDebit(Guid BetId, string OperatorId, string PlayerId, string Currency, Guid? RoundId, int Attempt = 0);
