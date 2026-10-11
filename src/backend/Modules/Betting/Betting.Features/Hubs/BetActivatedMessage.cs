namespace Apogeu.Betting.Features.Hubs;

/// <summary><c>BetActivated</c> (SPEC-0022 §6): a queued bet entered the betting phase of <paramref name="RoundId"/>.</summary>
public sealed record BetActivatedMessage(Guid BetId, Guid RoundId, long RoundNumber);
