using System.Globalization;
using System.Text.Json;

namespace Apogeu.BuildingBlocks.Contracts.Json;

/// <summary>
/// Decimal-string wire format shared by <see cref="Money"/> and <see cref="Multiplier"/> (ADR-0008): invariant culture,
/// at least two decimal places and never rounded, so <c>10.5m</c> travels as <c>"10.50"</c> and the value read back is exact.
/// </summary>
internal static class DecimalString
{
    /// <summary>Two decimal places always, further places only when the value has them (no silent rounding of money).</summary>
    private const string Format = "0.00############################";

    private const NumberStyles Styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static void Write(Utf8JsonWriter writer, decimal value) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));

    public static decimal Read(ref Utf8JsonReader reader, string typeName)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"{typeName} must be a JSON string with a decimal value (for example \"10.50\"), got {reader.TokenType}.");
        }

        var text = reader.GetString();
        if (!decimal.TryParse(text, Styles, CultureInfo.InvariantCulture, out var value))
        {
            throw new JsonException($"'{text}' is not a valid {typeName} decimal string (for example \"10.50\").");
        }

        return value;
    }
}
