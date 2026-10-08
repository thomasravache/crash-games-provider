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
}
