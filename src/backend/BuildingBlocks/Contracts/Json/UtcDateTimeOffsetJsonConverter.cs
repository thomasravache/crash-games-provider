using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Apogeu.BuildingBlocks.Contracts.Json;

/// <summary>
/// Serializes <see cref="DateTimeOffset"/> as ISO-8601 in UTC with milliseconds (<c>"2026-10-07T22:00:00.000Z"</c>), the
/// precision of a JavaScript <c>Date</c>; reads any ISO-8601 value with an offset.
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
    }
}
