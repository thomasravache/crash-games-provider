namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>The cash-out of a bet as persisted (SPEC-0023): repeated by a new <c>CashOut</c> of the same bet.</summary>
/// <param name="Multiplier">Multiplier of the cash-out.</param>
/// <param name="Payout">Payout credited.</param>
/// <param name="Auto">Whether it was the automatic one.</param>
/// <param name="Credited">Whether the wallet confirmed the credit.</param>
/// <param name="Balance">Balance after the credit, once confirmed.</param>
public sealed record CashOutRecord(decimal Multiplier, decimal Payout, bool Auto, bool Credited, decimal? Balance);
