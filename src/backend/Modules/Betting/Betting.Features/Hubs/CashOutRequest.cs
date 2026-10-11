namespace Apogeu.Betting.Features.Hubs;

/// <summary>Argument of <c>CashOut</c> (SPEC-0023 §6).</summary>
public sealed record CashOutRequest(Guid BetId);
