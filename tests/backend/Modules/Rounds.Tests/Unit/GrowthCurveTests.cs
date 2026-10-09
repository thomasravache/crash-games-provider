using Apogeu.Rounds.Domain;
using Xunit;

namespace Apogeu.Rounds.Tests.Unit;

public sealed class GrowthCurveTests
{
    private static readonly GrowthCurve Curve = new(0.00006);

    [Fact(DisplayName = "Dados k = 0,00006 e crash 2,00, quando calculado t_c, então ≈ 11.552,45 ms sem arredondar; m(t_c) = 2.00 pela regra t ≥ t_c ⇒ m = crash; e m(11.552 ms) exibido = 1.99")]
    [Trait("Spec", "SPEC-0008:UT-02")]
    public void GivenGrowthConstantAndCrashTwoWhenCrashTimeIsComputedThenItIsLnTwoOverKAndMultiplierFollowsTheRule()
    {
        var crashTime = Curve.TimeToCrash(2.00m);

        // ln(2) / 0.00006 = 11552.4530... ms: not rounded to whole milliseconds.
        Assert.InRange(crashTime.TotalMilliseconds, 11_552.44, 11_552.46);
        Assert.Equal(2.00m, Curve.MultiplierAt(crashTime, 2.00m));
        Assert.Equal(2.00m, Curve.MultiplierAt(crashTime + TimeSpan.FromSeconds(1), 2.00m));

        // e^(0.00006 · 11552) = 1.99995...: displayed as floor(m · 100) / 100.
        Assert.Equal(1.99m, Curve.MultiplierAt(TimeSpan.FromMilliseconds(11_552), 2.00m));
    }

    [Theory(DisplayName = "Dado k = 0,00006, quando o multiplicador exibido é calculado antes do crash, então é floor(e^(k·t)·100)/100")]
    [Trait("Spec", "SPEC-0008:UT-02")]
    [InlineData(0, "1.00")]
    [InlineData(5_000, "1.34")]
    [InlineData(10_000, "1.82")]
    public void GivenGrowthConstantWhenMultiplierIsComputedBeforeCrashThenItIsFlooredToHundredths(int milliseconds, string expected)
    {
        var multiplier = Curve.MultiplierAt(TimeSpan.FromMilliseconds(milliseconds), 10_000.00m);

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), multiplier);
    }
}
