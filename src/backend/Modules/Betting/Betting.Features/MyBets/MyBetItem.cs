using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.MyBets;

/// <summary>A bet of the session's player in <c>GET /api/v1/me/bets</c> (SPEC-0025 §6).</summary>
/// <param name="BetId">Id of the bet.</param>
/// <param name="RoundId">Round of the bet; <see langword="null"/> for a queued bet without a round.</param>
/// <param name="RoundNumber">Number of the round; <see langword="null"/> for a queued bet without a round.</param>
/// <param name="Panel">Panel 1 or 2.</param>
/// <param name="Amount">Amount of the bet.</param>
/// <param name="Currency">ISO 4217 currency of the bet.</param>
/// <param name="AutoCashOut">Automatic cash-out, when set.</param>
/// <param name="Multiplier">Multiplier of the cash-out, when cashed out.</param>
/// <param name="Payout">Payout of the cash-out, when cashed out.</param>
/// <param name="Status">Current status.</param>
/// <param name="PlacedAt">When the bet was accepted (UTC).</param>
public sealed record MyBetItem(
    Guid BetId,
    Guid? RoundId,
    long? RoundNumber,
    int Panel,
    Money Amount,
    string Currency,
    Multiplier? AutoCashOut,
    Multiplier? Multiplier,
    Money? Payout,
    MyBetStatus Status,
    DateTimeOffset PlacedAt);
