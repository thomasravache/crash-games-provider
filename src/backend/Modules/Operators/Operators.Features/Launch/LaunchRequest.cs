namespace Apogeu.Operators.Features.Launch;

/// <summary>
/// Body of <c>POST /api/v1/operator/launch</c> (SPEC-0009 §6). Every member is nullable so that a missing field reaches
/// <see cref="LaunchRequestValidator"/> and becomes <c>400 VALIDATION_ERROR</c> instead of a binding failure.
/// </summary>
public sealed record LaunchRequest(
    string? PlayerId,
    string? Nickname,
    string? Currency,
    string? GameCode,
    string? Mode,
    string? Language);
