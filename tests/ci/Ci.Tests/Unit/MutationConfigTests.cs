using Apogeu.Ci.Tests.Support;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Unit;

/// <summary>
/// UT-01 of SPEC-0016 (static part, normal suite): <c>stryker-config.json</c> mutates only Fairness.Domain, names only
/// test projects that exist, breaks below 85% (SPEC-0007 §7.6) and uses a runner/coverage mode that works with the
/// xUnit v3 test projects. The real run is <see cref="Nightly.MutationScoreTests"/>.
/// </summary>
public sealed class MutationConfigTests
{
    private readonly StrykerConfig _config = StrykerConfig.Load();

    [Fact(DisplayName = "Dado o stryker-config.json, quando lido, então todo projeto de teste listado existe")]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public void GivenStrykerConfigWhenReadThenEveryTestProjectExists()
    {
        Assert.NotEmpty(_config.TestProjects);
        var missing = _config.TestProjects.Where(project => !File.Exists(RepositoryRoot.Combine(project))).ToList();
        Assert.True(
            missing.Count == 0,
            $"{StrykerConfig.FileName} lists test project(s) that do not exist: {string.Join(", ", missing)}. "
            + "`dotnet stryker` with the repository config fails on them; tools/ci/stryker-targets filters them only in the nightly.");
    }

    [Fact(DisplayName = "Dado o stryker-config.json, quando lido, então só o Fairness.Domain é mutado")]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public void GivenStrykerConfigWhenReadThenOnlyFairnessDomainIsMutated()
    {
        Assert.NotEmpty(_config.MutateIncludes);
        var outside = _config.MutateIncludes
            .Where(glob => !glob.Contains(StrykerConfig.MutatedProjectSegment, StringComparison.Ordinal))
            .ToList();
        Assert.True(
            outside.Count == 0,
            $"{StrykerConfig.FileName} `mutate` must target only `{StrykerConfig.MutatedProjectSegment}` (SPEC-0007 §7.6); "
            + $"globs outside it: {string.Join(", ", outside)}");
    }

    [Fact(DisplayName = "Dado o stryker-config.json, quando lido, então o break é de pelo menos 85%")]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public void GivenStrykerConfigWhenReadThenBreakIsAtLeastTheSpecMinimum()
    {
        Assert.True(
            _config.Break >= StrykerConfig.MinimumScore,
            $"{StrykerConfig.FileName} `thresholds.break` is {_config.Break?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "absent"}; "
            + $"SPEC-0007 §7.6 requires a mutation score of at least {StrykerConfig.MinimumScore}% on Fairness.Domain.");
    }

    [Fact(DisplayName = "Dado o stryker-config.json, quando lido, então o runner captura cobertura com xUnit v3 (runner mtp ou coverage-analysis off)")]
    [Trait("Spec", "SPEC-0016:UT-01")]
    public void GivenStrykerConfigWhenReadThenRunnerIsCompatibleWithXunitV3()
    {
        var mtp = string.Equals(_config.TestRunner, "mtp", StringComparison.OrdinalIgnoreCase);
        var coverageOff = string.Equals(_config.CoverageAnalysis, "off", StringComparison.OrdinalIgnoreCase);
        Assert.True(
            mtp || coverageOff,
            $"{StrykerConfig.FileName} uses test-runner `{_config.TestRunner ?? "vstest (default)"}` with coverage-analysis "
            + $"`{_config.CoverageAnalysis ?? "perTest (default)"}`: with xUnit v3 the vstest runner cannot capture per-test coverage "
            + "(\"test coverage capture failed\", every mutant NoCoverage). Use `test-runner: mtp` or `coverage-analysis: off` (SPEC-0016 §3).");
    }
}
