using System.Text.RegularExpressions;

namespace Apogeu.DemoCasino.Session;

/// <summary>
/// The entry rules of SPEC-0043 §6: nickname of 3–20 letters, digits or _ and currency BRL only. Returns the error code of
/// the contract, or null when the entry is valid.
/// </summary>
internal static partial class SessionEntry
{
    public const string SupportedCurrency = "BRL";
    public const string InvalidNickname = "INVALID_NICKNAME";
    public const string CurrencyNotSupported = "CURRENCY_NOT_SUPPORTED";

    public static string? Validate(string? nickname, string? currency) =>
        (nickname, currency) switch
        {
            (null, _) => InvalidNickname,
            _ when !NicknamePattern().IsMatch(nickname) => InvalidNickname,
            (_, SupportedCurrency) => null,
            _ => CurrencyNotSupported,
        };

    /// <summary>A new player id of the demo casino: <c>demo-&lt;uuid&gt;</c>.</summary>
    public static string NewPlayerId() => "demo-" + Guid.NewGuid().ToString("D");

    [GeneratedRegex("^[A-Za-z0-9_]{3,20}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex NicknamePattern();
}
