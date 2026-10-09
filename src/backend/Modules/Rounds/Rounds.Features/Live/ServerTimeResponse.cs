using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary>Answer of <c>GetServerTime</c> (clock offset of the client).</summary>
public sealed record ServerTimeResponse(DateTimeOffset ServerTime);
