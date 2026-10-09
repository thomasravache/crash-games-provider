using Apogeu.BuildingBlocks.Security;

namespace Apogeu.Operators.Features.Launch;

/// <summary>
/// Validates a launch request (SPEC-0009 §6): missing or unsupported fields are <c>VALIDATION_ERROR</c>; any mode other than
/// <c>demo</c> is <c>REAL_MONEY_DISABLED</c> (ADR-0020: no real money before the compliance spec).
/// </summary>
public static class LaunchRequestValidator
{
    /// <summary>The only mode accepted in the demo phase (ADR-0020).</summary>
    public const string DemoMode = "demo";

    public const string SupportedCurrency = "BRL";

    public const string SupportedGameCode = "pipa";

    public const string SupportedLanguage = "pt-BR";

    public const int MaxPlayerIdLength = 128;

    public const int MaxNicknameLength = 32;

    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);

    public static LaunchRequestValidation Validate(LaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Real money is refused by rule, whatever else the request carries (ADR-0020).
        if (!string.IsNullOrEmpty(request.Mode) && !string.Equals(request.Mode, DemoMode, StringComparison.Ordinal))
        {
            return new LaunchRequestValidation(
                SecurityErrorCodes.RealMoneyDisabled,
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["mode"] = [$"Only '{DemoMode}' is enabled."] });
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        RequireText(errors, "playerId", request.PlayerId, MaxPlayerIdLength);
        if (request.Nickname is not null && (request.Nickname.Length > MaxNicknameLength || HasControlCharacter(request.Nickname)))
        {
            errors["nickname"] = [$"At most {MaxNicknameLength} printable characters."];
        }

        RequireValue(errors, "currency", request.Currency, SupportedCurrency);
        RequireValue(errors, "gameCode", request.GameCode, SupportedGameCode);
        RequireValue(errors, "mode", request.Mode, DemoMode);
        RequireValue(errors, "language", request.Language, SupportedLanguage);

        return errors.Count == 0
            ? new LaunchRequestValidation(null, NoErrors)
            : new LaunchRequestValidation(SecurityErrorCodes.ValidationError, errors);
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["Required."];
        }
        else if (value.Length > maxLength || HasControlCharacter(value))
        {
            errors[field] = [$"At most {maxLength} printable characters."];
        }
    }

    private static void RequireValue(Dictionary<string, string[]> errors, string field, string? value, string supported)
    {
        if (string.IsNullOrEmpty(value))
        {
            errors[field] = ["Required."];
        }
        else if (!string.Equals(value, supported, StringComparison.Ordinal))
        {
            errors[field] = [$"Only '{supported}' is supported."];
        }
    }

    private static bool HasControlCharacter(string value) => value.Any(char.IsControl);
}
