namespace Apogeu.Betting.Features.Hubs;

/// <summary>Argument of <c>CancelBet</c> (SPEC-0022 §6).</summary>
public sealed record CancelBetRequest(Guid BetId);
