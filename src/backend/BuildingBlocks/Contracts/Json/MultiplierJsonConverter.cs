#pragma warning disable MA0025 // Scaffold (SPEC-0006): the Implementer replaces the stub in the Green phase.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Apogeu.BuildingBlocks.Contracts.Json;

/// <summary>Serializes <see cref="Multiplier"/> as a decimal string (<c>"2.47"</c>).</summary>
public sealed class MultiplierJsonConverter : JsonConverter<Multiplier>
{
    public override Multiplier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotImplementedException();

    public override void Write(Utf8JsonWriter writer, Multiplier value, JsonSerializerOptions options) =>
        throw new NotImplementedException();
}
