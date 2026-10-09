using Apogeu.Operators.Features.Launch;
using Xunit;

namespace Apogeu.Operators.Tests.Unit;

/// <summary>UT-04: the demo-only rule (ADR-0020) and the field validation of the launch request (SPEC-0009 §6).</summary>
public sealed class LaunchRequestValidatorTests
{
    private static readonly LaunchRequest Valid = new("player-ut04", "Pipeiro", "BRL", "pipa", "demo", "pt-BR");

    [Theory(DisplayName = "Dado mode diferente de demo, quando o lançamento é validado, então REAL_MONEY_DISABLED")]
    [Trait("Spec", "SPEC-0009:UT-04")]
    [InlineData("real")]
    [InlineData("REAL")]
    [InlineData("Real")]
    [InlineData("fun")]
    public void GivenNonDemoModeWhenLaunchIsValidatedThenRealMoneyDisabled(string mode)
    {
        var result = LaunchRequestValidator.Validate(Valid with { Mode = mode });

        Assert.False(result.IsValid);
        Assert.Equal("REAL_MONEY_DISABLED", result.ErrorCode);
    }

    [Fact(DisplayName = "Dado um lançamento demo completo, quando validado, então é válido")]
    [Trait("Spec", "SPEC-0009:UT-04")]
    public void GivenCompleteDemoLaunchWhenValidatedThenItIsValid()
    {
        var result = LaunchRequestValidator.Validate(Valid);

        Assert.True(result.IsValid, $"Expected a valid demo launch, got {result.ErrorCode}.");
        Assert.Empty(result.Errors);

        Assert.True(LaunchRequestValidator.Validate(Valid with { Nickname = null }).IsValid, "The nickname is optional (SPEC-0009 §6).");
    }

    [Fact(DisplayName = "Dado um lançamento sem playerId, quando validado, então VALIDATION_ERROR apontando o campo")]
    [Trait("Spec", "SPEC-0009:UT-04")]
    public void GivenLaunchWithoutPlayerIdWhenValidatedThenValidationError()
    {
        var result = LaunchRequestValidator.Validate(Valid with { PlayerId = null });

        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Contains(result.Errors.Keys, key => string.Equals(key, "playerId", StringComparison.OrdinalIgnoreCase));
    }
}
