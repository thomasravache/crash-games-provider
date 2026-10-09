using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary>The flight started: the client interpolates <c>m(t) = e^(k·t)</c> from <c>startedAt</c>.</summary>
public sealed record RoundFlyingMessage(Guid RoundId, DateTimeOffset StartedAt, string GrowthK);
