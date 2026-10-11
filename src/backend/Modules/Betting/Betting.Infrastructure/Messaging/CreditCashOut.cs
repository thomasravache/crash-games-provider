namespace Apogeu.Betting.Infrastructure.Messaging;

/// <summary>
/// Durable command (Wolverine, ADR-0002) to credit the payout of a cash-out with <c>CreditAsync</c> and the deterministic id
/// <c>bet:{betId}:credit</c> until the wallet confirms it (ADR-0007, SPEC-0023). It is stored, scheduled, in the same
/// transaction as the cash-out: when the credit tried at once is confirmed, it finds nothing left to do.
/// </summary>
/// <param name="BetId">The bet cashed out.</param>
/// <param name="OperatorId">Operator of the player.</param>
/// <param name="PlayerId">Pseudonymous player id.</param>
/// <param name="Currency">Currency of the account.</param>
/// <param name="RoundId">Round of the bet.</param>
/// <param name="Payout">Amount credited.</param>
/// <param name="Attempt">How many times the wallet did not confirm it yet (backoff of the next attempt).</param>
/// <param name="Announce">
/// Whether the first attempt also tells the player the cash-out (<c>CashedOut</c>, credited or pending): an automatic cash-out
/// stored by the round events, whose queue never waits for the wallet (review G4, M4).
/// </param>
public sealed record CreditCashOut(Guid BetId, string OperatorId, string PlayerId, string Currency, Guid RoundId, decimal Payout, int Attempt = 0, bool Announce = false);
