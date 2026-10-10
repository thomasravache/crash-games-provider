using Apogeu.Ci.Tests.Support;
using Xunit;
using NpmPackage = Apogeu.Ci.Tests.Support.LicenseFixtureDirectory.NpmPackage;

namespace Apogeu.Ci.Tests.Unit;

/// <summary>
/// IMP-01 of SPEC-0011 (emenda do ADR-0022): tools/ci/check-licenses accepts the GSAP Standard License only for the
/// package <c>gsap</c>, also as a production dependency; the other rules of ADR-0016/ADR-0022 are unchanged. The
/// script runs against a fixture (fake <c>pnpm licenses list --json</c>, no NuGet package).
/// </summary>
public sealed class LicenseCheckTests
{
    /// <summary>Licence string reported by pnpm for gsap 3.15.0 (package.json of the published package).</summary>
    private const string GsapStandardLicense = "Standard 'no charge' license: https://gsap.com/standard-license";

    [Theory(DisplayName = "Dado o pacote gsap com a GSAP Standard License, quando tools/ci/check-licenses roda, então sai com código 0")]
    [Trait("Spec", "SPEC-0011:IMP-01")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenGsapWithStandardLicenseWhenCheckLicensesRunsThenItSucceeds(bool production)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = LicenseFixtureDirectory.Create([new NpmPackage("gsap", "3.15.0", GsapStandardLicense, production)]);

        var result = await fixture.RunCheckLicensesAsync(cancellationToken);

        Assert.True(
            result.Succeeded,
            $"check-licenses rejected gsap under the GSAP Standard License (production={production}), allowed by ADR-0022. " + result.Describe());
    }

    [Theory(DisplayName = "Dado outro pacote com a GSAP Standard License, quando tools/ci/check-licenses roda, então falha apontando o pacote")]
    [Trait("Spec", "SPEC-0011:IMP-01")]
    [InlineData("not-gsap", true)]
    [InlineData("not-gsap", false)]
    [InlineData("@gsap/react", true)]
    public async Task GivenOtherPackageWithGsapStandardLicenseWhenCheckLicensesRunsThenItFails(string name, bool production)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = LicenseFixtureDirectory.Create(
        [
            new NpmPackage("gsap", "3.15.0", GsapStandardLicense, Production: true),
            new NpmPackage(name, "1.0.0", GsapStandardLicense, production),
        ]);

        var result = await fixture.RunCheckLicensesAsync(cancellationToken);

        AssertRejectedOnly(result, name);
    }

    [Fact(DisplayName = "Dado um pacote MIT de produção, quando tools/ci/check-licenses roda, então sai com código 0")]
    [Trait("Spec", "SPEC-0011:IMP-01")]
    public async Task GivenMitPackageWhenCheckLicensesRunsThenItSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = LicenseFixtureDirectory.Create([new NpmPackage("left-pad", "1.3.0", "MIT", Production: true)]);

        var result = await fixture.RunCheckLicensesAsync(cancellationToken);

        Assert.True(result.Succeeded, "check-licenses rejected an MIT package. " + result.Describe());
    }

    [Fact(DisplayName = "Dado um pacote GPL-3.0 de produção, quando tools/ci/check-licenses roda, então falha apontando o pacote")]
    [Trait("Spec", "SPEC-0011:IMP-01")]
    public async Task GivenGplPackageWhenCheckLicensesRunsThenItFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = LicenseFixtureDirectory.Create(
        [
            new NpmPackage("left-pad", "1.3.0", "MIT", Production: true),
            new NpmPackage("copyleft-lib", "2.0.0", "GPL-3.0", Production: true),
        ]);

        var result = await fixture.RunCheckLicensesAsync(cancellationToken);

        AssertRejectedOnly(result, "copyleft-lib");
    }

    /// <summary>The run fails because of <paramref name="name"/> and of no other package (not a crash of the script).</summary>
    private static void AssertRejectedOnly(Repository.Tests.Support.ProcessResult result, string name)
    {
        Assert.False(result.TimedOut, "check-licenses timed out. " + result.Describe());
        Assert.True(result.ExitCode == 1, "check-licenses should exit with code 1. " + result.Describe());

        var failures = result.StandardOutput
            .Split('\n')
            .Where(line => line.StartsWith("FAIL licence not allowed", StringComparison.Ordinal))
            .ToList();
        Assert.True(
            failures.Count == 1 && failures[0].Contains($"npm {name} ", StringComparison.Ordinal),
            $"check-licenses should reject {name} and nothing else. " + result.Describe());
    }
}
