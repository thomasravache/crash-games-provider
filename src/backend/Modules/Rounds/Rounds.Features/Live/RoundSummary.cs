using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary>One crashed round of the history.</summary>
public sealed record RoundSummary(Guid RoundId, long RoundNumber, Multiplier CrashPoint);
