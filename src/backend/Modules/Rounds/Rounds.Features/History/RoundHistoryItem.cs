using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Rounds.Features.History;

/// <summary>One item of <c>GET /api/v1/salas/{salaId}/rounds?limit=1..100</c>.</summary>
public sealed record RoundHistoryItem(Guid RoundId, long RoundNumber, Multiplier CrashPoint, DateTimeOffset CrashedAt);
