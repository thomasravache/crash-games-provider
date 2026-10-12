namespace Apogeu.Betting.Infrastructure.Messaging;

/// <summary>
/// Durable command (Wolverine, ADR-0002) to refund a bet of an annulled round (SPEC-0024) with <c>RollbackAsync</c> and the
/// deterministic id <c>bet:{betId}:rollback</c> until the wallet confirms it (ADR-0007). It is stored in the same transaction
/// that makes the bet refunded and runs at once on its own queue (the round events never wait for the wallet).
/// </summary>
/// <param name="BetId">The bet refunded.</param>
/// <param name="OperatorId">Operator of the player.</param>
/// <param name="PlayerId">Pseudonymous player id.</param>
/// <param name="Currency">Currency of the account.</param>
/// <param name="RoundId">The annulled round.</param>
/// <param name="Amount">Amount refunded: the bet's amount.</param>
/// <param name="Attempt">How many times the wallet did not confirm it yet (backoff of the next attempt).</param>
/// <param name="Announce">Whether this attempt tells the player <c>BetRefunded</c> (the first one only).</param>
public sealed record RefundBet(Guid BetId, string OperatorId, string PlayerId, string Currency, Guid RoundId, decimal Amount, int Attempt = 0, bool Announce = true);
