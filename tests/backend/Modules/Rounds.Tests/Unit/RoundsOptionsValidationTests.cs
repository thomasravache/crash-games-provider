using System.Globalization;
using Apogeu.Rounds.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Apogeu.Rounds.Tests.Unit;

/// <summary>
/// RTP and cap of a room have 2 decimal places (SPEC-0008 §6: <c>rtp</c> "0.97", <c>cap</c> as Multiplier): a value with more
/// places would be published rounded in the snapshot and the verification data, diverging from the crash points computed
/// with the exact value (G4 review). The validator the module registers rejects it at startup.
/// </summary>
public sealed class RoundsOptionsValidationTests
{
    [Theory(DisplayName = "Dadas as opções de uma sala, quando validadas na partida, então RTP e limite com mais de 2 casas decimais são rejeitados e 0.97 / 10.00 aceitos")]
    [Trait("Spec", "SPEC-0008:IT-02")]
    [InlineData("0.97", "10.00", true)]
    [InlineData("0.90", "10000.00", true)]
    [InlineData("0.965", "10.00", false)]
    [InlineData("0.97", "10.005", false)]
    public void GivenSalaOptionsWhenValidatedThenRtpAndCapBeyondTwoDecimalsAreRejected(string rtp, string cap, bool accepted)
    {
        var options = new RoundsOptions();
        options.Salas["demo"] = new SalaOptions
        {
            Rtp = decimal.Parse(rtp, CultureInfo.InvariantCulture),
            Cap = decimal.Parse(cap, CultureInfo.InvariantCulture),
        };

        var failures = Validate(options);

        if (accepted)
        {
            Assert.True(failures.Count == 0, $"RTP {rtp} and cap {cap} must be accepted: {string.Join(" | ", failures)}");
        }
        else
        {
            Assert.True(failures.Count > 0, $"RTP {rtp} and cap {cap} must be rejected: they have more than 2 decimal places.");
            Assert.Contains(failures, f => f.Contains("Rounds:Salas:demo", StringComparison.Ordinal));
        }
    }

    /// <summary>Runs every <see cref="IValidateOptions{TOptions}"/> the module registers (the real validator, not a copy).</summary>
    private static List<string> Validate(RoundsOptions options)
    {
        using var provider = new ServiceCollection().AddRoundsModule().BuildServiceProvider();
        var failures = new List<string>();
        foreach (var validator in provider.GetServices<IValidateOptions<RoundsOptions>>())
        {
            var result = validator.Validate(Options.DefaultName, options);
            if (result.Failed)
            {
                failures.AddRange(result.Failures);
            }
        }

        return failures;
    }
}
