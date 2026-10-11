using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>Result of <c>GetBetLimits</c> (SPEC-0022 §6): the limits of the session's operator in its currency.</summary>
public sealed record BetLimitsResponse(string Currency, Money MinBet, Money MaxBet, Money MaxWin);
