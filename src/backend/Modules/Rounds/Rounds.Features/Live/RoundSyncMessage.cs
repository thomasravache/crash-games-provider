using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary>Every 200 ms of flight: the multiplier at the server time.</summary>
public sealed record RoundSyncMessage(Guid RoundId, DateTimeOffset ServerTime, Multiplier Multiplier);
