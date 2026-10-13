using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>A cash-out of the current round as the room sees it (SPEC-0025 §6).</summary>
/// <param name="BetRef">Opaque public reference of the bet, the same of its <see cref="LiveBet"/>.</param>
/// <param name="Multiplier">Multiplier of the cash-out.</param>
/// <param name="Payout">Payout of the cash-out.</param>
public sealed record LiveCashOut(string BetRef, Multiplier Multiplier, Money Payout);
