namespace Apogeu.Operators.Features.Catalog;

/// <summary>An item of <c>200</c> of <c>GET /api/v1/games</c>: a game enabled for the calling operator (SPEC-0020 §6).</summary>
public sealed record GameSummaryResponse(string Code, string Name, IReadOnlyList<string> Currencies);
