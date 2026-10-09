using System.Text.Json;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.BuildingBlocks.Contracts.Json;
using Apogeu.Contracts.Tests.Support;
using Xunit;

namespace Apogeu.Contracts.Tests.Unit;

public sealed class ContractJsonConvertersTests
{
    [Fact(DisplayName = "Dado um DTO com dinheiro 10.5 e multiplicador 2.47, quando serializado pelos conversores, então gera \"10.50\" e \"2.47\" como string")]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void GivenDtoWithMoneyAndMultiplierWhenSerializedThenBothAreDecimalStrings()
    {
        var dto = ContractFixtureDto.Sample with { FixtureStake = new Money(10.5m), FixtureCashout = new Multiplier(2.47m) };

        var json = JsonSerializer.Serialize(dto, ContractJson.Options);

        using var document = JsonDocument.Parse(json);
        var stake = document.RootElement.GetProperty("fixtureStake");
        var cashout = document.RootElement.GetProperty("fixtureCashout");
        Assert.True(stake.ValueKind == JsonValueKind.String, $"Money must travel as a JSON string, got {stake.ValueKind}: {json}");
        Assert.Equal("10.50", stake.GetString());
        Assert.True(cashout.ValueKind == JsonValueKind.String, $"Multiplier must travel as a JSON string, got {cashout.ValueKind}: {json}");
        Assert.Equal("2.47", cashout.GetString());
    }

    [Fact(DisplayName = "Dado o JSON com \"10.50\" e \"2.47\", quando desserializado pelos conversores, então devolve os mesmos valores exatos")]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void GivenSerializedMoneyAndMultiplierWhenDeserializedThenValuesRoundTripExactly()
    {
        var original = ContractFixtureDto.Sample with { FixtureStake = new Money(10.5m), FixtureCashout = new Multiplier(2.47m) };
        var json = JsonSerializer.Serialize(original, ContractJson.Options);

        var roundTripped = JsonSerializer.Deserialize<ContractFixtureDto>(json, ContractJson.Options);

        Assert.NotNull(roundTripped);
        Assert.Equal(10.5m, roundTripped.FixtureStake.Value);
        Assert.Equal(2.47m, roundTripped.FixtureCashout.Value);
        Assert.Equal(original, roundTripped);
    }

    [Fact(DisplayName = "Dado uma data ISO-8601 sem offset, quando desserializada pelos conversores, então lança JsonException")]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void GivenTimestampWithoutOffsetWhenDeserializedThenJsonExceptionIsThrown()
    {
        var exception = Record.Exception(() => JsonSerializer.Deserialize<DateTimeOffset>("\"2026-10-07T22:00:00\"", ContractJson.Options));

        Assert.True(
            exception is JsonException,
            $"A timestamp without 'Z' or an offset is ambiguous and must be rejected with JsonException (SPEC-0006 §6: ISO-8601 UTC); got {exception?.GetType().Name ?? "no exception"}.");
    }

    [Theory(DisplayName = "Dado uma data ISO-8601 com Z ou offset, quando desserializada pelos conversores, então preserva o instante")]
    [Trait("Spec", "SPEC-0006:UT-01")]
    [InlineData("\"2026-10-07T22:00:00.000Z\"")]
    [InlineData("\"2026-10-08T01:00:00+03:00\"")]
    public void GivenTimestampWithOffsetWhenDeserializedThenInstantIsPreserved(string json)
    {
        var value = JsonSerializer.Deserialize<DateTimeOffset>(json, ContractJson.Options);

        Assert.Equal(new DateTimeOffset(2026, 10, 7, 22, 0, 0, TimeSpan.Zero), value);
    }

    [Fact(DisplayName = "Dado um DateTimeOffset com offset, quando serializado pelos conversores, então gera ISO-8601 UTC com milissegundos e Z")]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void GivenDateTimeOffsetWithOffsetWhenSerializedThenUtcWithMillisecondsAndZ()
    {
        var value = new DateTimeOffset(2026, 10, 8, 1, 0, 0, 120, TimeSpan.FromHours(3));

        var json = JsonSerializer.Serialize(value, ContractJson.Options);

        Assert.Equal("\"2026-10-07T22:00:00.120Z\"", json);
    }

    [Fact(DisplayName = "Dado um enum, quando serializado e desserializado pelos conversores, então viaja como string camelCase e rejeita número")]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public void GivenEnumWhenSerializedThenCamelCaseStringAndIntegerIsRejected()
    {
        var json = JsonSerializer.Serialize(HubFixturePhase.InFlight, ContractJson.Options);

        Assert.Equal("\"inFlight\"", json);
        Assert.Equal(HubFixturePhase.InFlight, JsonSerializer.Deserialize<HubFixturePhase>("\"inFlight\"", ContractJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<HubFixturePhase>("1", ContractJson.Options));
    }
}
