using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary>Public configuration of the room.</summary>
public sealed record SalaInfo(string Id, string Rtp, Multiplier Cap, string GrowthK);
