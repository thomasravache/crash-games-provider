using Apogeu.Betting.Domain;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

/// <summary>UT-01 (SPEC-0025 §6, ADR-0005): the nickname a room shows is anonymized for every player.</summary>
public sealed class LiveBetNicknameTests
{
    [Theory(DisplayName = "Dados os apelidos \"Marcos\", \"A\" e ausente, quando anonimizados, então \"Ma***\", \"A***\" e \"Jogador***\"")]
    [Trait("Spec", "SPEC-0025:UT-01")]
    [InlineData("Marcos", "Ma***")]
    [InlineData("A", "A***")]
    [InlineData(null, "Jogador***")]
    public void GivenNicknameWhenAnonymizedThenTwoFirstLettersAndMask(string? nickname, string expected) =>
        Assert.Equal(expected, LiveBetNickname.Anonymize(nickname));
}
