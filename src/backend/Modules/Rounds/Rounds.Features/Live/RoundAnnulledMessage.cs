using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary>The round was annulled (ADR-0018).</summary>
public sealed record RoundAnnulledMessage(Guid RoundId, string Reason);
