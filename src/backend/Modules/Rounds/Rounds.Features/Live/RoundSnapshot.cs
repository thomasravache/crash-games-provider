using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary>Answer of <c>JoinSala</c>: the room, the current round (null before the first one) and the last 20 crashed rounds.</summary>
public sealed record RoundSnapshot(SalaInfo Sala, CurrentRound? Current, IReadOnlyList<RoundSummary> History);
