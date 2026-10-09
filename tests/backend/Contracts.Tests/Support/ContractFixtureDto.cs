using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>
/// DTO-fixture exposed only by the tests (SPEC-0006 §7: "DTOs-fixture no projeto de testes"): money, multiplier,
/// a nullable string and a non-nullable string as control.
/// </summary>
public sealed record ContractFixtureDto(Money FixtureStake, Multiplier FixtureCashout, string? FixtureNickname, string FixtureLabel)
{
    public static ContractFixtureDto Sample { get; } =
        new(new Money(10.5m), new Multiplier(2.47m), FixtureNickname: null, FixtureLabel: "fixture");
}
