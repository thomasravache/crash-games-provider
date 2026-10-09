using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>
/// Client contract of <see cref="FixtureHub"/> (SPEC-0006 §7: fixtures live in the test project): methods the server
/// calls on the client, with contract scalars and a nullable string.
/// </summary>
public interface IFixtureClient
{
    Task FixtureRoundStarted(Money stake, Multiplier target);

    Task FixtureNicknameChanged(string? nickname);
}
