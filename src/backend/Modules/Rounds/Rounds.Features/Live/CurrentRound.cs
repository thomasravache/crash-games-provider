using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary>The current round; crashPoint and revealedSeed are null until the crash.</summary>
public sealed record CurrentRound(
    Guid RoundId,
    long RoundNumber,
    RoundState State,
    string Commitment,
    DateTimeOffset BettingEndsAt,
    DateTimeOffset? StartedAt,
    Multiplier? CrashPoint,
    DateTimeOffset? CrashedAt,
    string? RevealedSeed);
