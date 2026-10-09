namespace Apogeu.Operators.Features.Sessions;

/// <summary>Body of <c>POST /api/v1/sessions</c> (SPEC-0009 §6).</summary>
public sealed record CreateSessionRequest(string? LaunchToken);
