namespace Apogeu.Operators.Features.Backoffice;

/// <summary>Body of <c>200</c> of <c>GET /api/v1/backoffice/me</c> (SPEC-0009 §6).</summary>
public sealed record MeResponse(string UserId, IReadOnlyList<string> Roles, string? OperatorId);
