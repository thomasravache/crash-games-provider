namespace Apogeu.Operators.Features.Launch;

/// <summary>Result of <see cref="LaunchRequestValidator.Validate"/>: the contract error code and the invalid fields, if any.</summary>
public sealed record LaunchRequestValidation(string? ErrorCode, IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => ErrorCode is null;
}
