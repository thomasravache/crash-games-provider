using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.Live;

/// <summary>The round crashed; its seed is revealed.</summary>
public sealed record RoundCrashedMessage(Guid RoundId, Multiplier CrashPoint, DateTimeOffset CrashedAt, string RevealedSeed);
