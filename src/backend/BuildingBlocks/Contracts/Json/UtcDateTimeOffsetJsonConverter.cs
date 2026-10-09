using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Apogeu.BuildingBlocks.Contracts.Json;

/// <summary>
/// Serializes <see cref="DateTimeOffset"/> as ISO-8601 in UTC with milliseconds (<c>"2026-10-07T22:00:00.000Z"</c>), the
/// precision of a JavaScript <c>Date</c>; reads any ISO-8601 value with <c>Z</c> or an explicit offset and rejects one
/// without either, which would be ambiguous (local time of whom?).
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // The reader reports DateTimeKind.Unspecified exactly when the ISO-8601 value carries neither 'Z' nor an offset.
        if (reader.GetDateTime().Kind == DateTimeKind.Unspecified)
        {
            throw new JsonException("Timestamps must be ISO-8601 with 'Z' or an explicit offset (e.g. \"2026-10-07T22:00:00.000Z\").");
        }

        return reader.GetDateTimeOffset();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
    }
}
