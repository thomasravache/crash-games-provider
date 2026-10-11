namespace Apogeu.Betting.Infrastructure.Messaging;

/// <summary>
/// Durable command (Wolverine, ADR-0002) to have the leader decide a manual cash-out it did not decide within its deadline
/// (<c>CashOutPending</c>, SPEC-0023 §4), with the reception instant recorded on the bet; stored in the same transaction as
/// that instant and retried until decided.
/// </summary>
/// <param name="BetId">The bet whose cash-out is pending.</param>
/// <param name="Attempt">How many times the leader could not be reached yet (backoff of the next attempt).</param>
public sealed record DecideCashOut(Guid BetId, int Attempt = 0);
