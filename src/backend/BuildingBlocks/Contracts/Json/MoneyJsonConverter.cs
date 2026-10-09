using System.Text.Json;
using System.Text.Json.Serialization;

namespace Apogeu.BuildingBlocks.Contracts.Json;

/// <summary>Serializes <see cref="Money"/> as a decimal string with two decimal places (<c>"10.50"</c>).</summary>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(DecimalString.Read(ref reader, nameof(Money)));

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        DecimalString.Write(writer, value.Value);
    }
}
