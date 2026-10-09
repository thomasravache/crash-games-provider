namespace Apogeu.Operators.Features.Sessions;

/// <summary>Body of <c>200</c> of <c>POST /api/v1/sessions</c> (SPEC-0009 §6).</summary>
public sealed record SessionResponse(
    string SessionToken,
    DateTimeOffset ExpiresAt,
    SessionPlayer Player,
    string SalaId,
    string Currency,
    string Mode);
