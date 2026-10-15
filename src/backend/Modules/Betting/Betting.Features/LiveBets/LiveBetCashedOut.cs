using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.LiveBets;

/// <summary>A cash-out of a bet of a round, as fed to the <see cref="LiveBetsBatcher"/> (SPEC-0025 §6).</summary>
/// <param name="SalaId">Room of the round.</param>
/// <param name="RoundId">Round of the bet.</param>
/// <param name="BetId">Internal id of the bet; the batcher maps it to the <c>betRef</c> of its <see cref="LiveBetPlaced"/>.</param>
/// <param name="Multiplier">Multiplier of the cash-out.</param>
/// <param name="Payout">Payout of the cash-out.</param>
public sealed record LiveBetCashedOut(string SalaId, Guid RoundId, Guid BetId, Multiplier Multiplier, Money Payout);
