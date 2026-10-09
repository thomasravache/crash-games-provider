namespace Apogeu.Operators.Features.Launch;

/// <summary>Who a launch is for (SPEC-0009 §5: only the pseudonymous id, optional nickname, currency and language).</summary>
public sealed record LaunchSubject(
    string OperatorId,
    string SalaId,
    string PlayerId,
    string? Nickname,
    string Currency,
    string Mode,
    string Language);
