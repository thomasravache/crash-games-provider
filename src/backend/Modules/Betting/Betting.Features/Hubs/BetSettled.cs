namespace Apogeu.Betting.Features.Hubs;

/// <summary><c>BetSettled</c> (SPEC-0024 §6): the bet's round crashed while it was active without a cash-out.</summary>
/// <param name="BetId">The bet.</param>
/// <param name="RoundId">Its round (the bare <see cref="Guid"/> on the wire).</param>
/// <param name="Status"><c>lost</c>.</param>
public sealed record BetSettled(Guid BetId, Guid RoundId, BetSettledStatus Status);
