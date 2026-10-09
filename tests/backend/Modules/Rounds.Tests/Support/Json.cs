using System.Globalization;
using System.Text.Json;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>Readers of the contract JSON (SPEC-0006 conventions: decimals as strings, instants as ISO-8601 UTC).</summary>
internal static class Json
{
    public static decimal Decimal(JsonElement element, string property) =>
        decimal.Parse(element.GetProperty(property).GetString()!, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    public static DateTimeOffset Instant(JsonElement element, string property) =>
        DateTimeOffset.Parse(element.GetProperty(property).GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    public static string Text(JsonElement element, string property) => element.GetProperty(property).GetString()!;

    /// <summary>Whether the property is absent or null.</summary>
    public static bool IsNullOrMissing(JsonElement element, string property) =>
        !element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}
