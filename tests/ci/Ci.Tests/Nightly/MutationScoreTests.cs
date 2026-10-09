using System.Globalization;
using Apogeu.Ci.Tests.Support;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Nightly;

/// <summary>
/// UT-01 of SPEC-0016 (real run, Category=Nightly): <c>dotnet stryker</c> with the repository config produces a JSON
/// report in which the public vectors cover the mutants of <c>SeedChain.cs</c> and Fairness.Domain scores at least
/// 85% (evidence of SPEC-0007 §7.6). Mutants that Stryker marks as <c>Ignored</c> (block filter) are left out of both
/// checks, as Stryker leaves them out of its score (SPEC-0016 amendment 2). Slow: restores the local tools, builds and runs the tests once per mutant.
/// </summary>
public sealed class MutationScoreTests
{
    public const string Category = "Nightly";

    /// <summary>Run by the nightly `mutation` job only; `rtp-simulation` filters it out (SPEC-0016 amendment 1).</summary>
    public const string MutationCategory = "Mutation";

    private static readonly TimeSpan StrykerTimeout = TimeSpan.FromMinutes(90);

    [Fact(DisplayName = "Dado o Fairness.Tests com os vetores públicos, quando o dotnet stryker roda no Fairness.Domain, então o JSON mostra os mutantes de SeedChain.cs cobertos e score ≥ 85%")]
    [Trait("Spec", "SPEC-0016:UT-01")]
    [Trait("Category", Category)]
    [Trait("Category", MutationCategory)]
    public async Task GivenPublicVectorsWhenStrykerRunsOnFairnessDomainThenSeedChainMutantsAreCoveredAndScoreIsAtLeast85()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = RepositoryRoot.Path;
        var output = Path.Combine(Path.GetTempPath(), "apogeu-stryker-" + Guid.NewGuid().ToString("N"));

        try
        {
            var restore = await ProcessRunner.DotnetAsync(root, ["tool", "restore"], cancellationToken);
            Assert.True(restore.Succeeded, restore.Describe());

            var stryker = await ProcessRunner.RunAsync(
                "dotnet",
                ["stryker", "--config-file", StrykerConfig.FileName, "--output", output, "--reporter", "json"],
                root,
                StrykerTimeout,
                cancellationToken: cancellationToken);
            TestContext.Current.TestOutputHelper?.WriteLine(stryker.Describe());

            var reportPath = Path.Combine(output, "reports", "mutation-report.json");
            Assert.True(File.Exists(reportPath), $"Stryker wrote no JSON report at {reportPath}. {stryker.Describe()}");

            var report = MutationReport.Load(reportPath);
            var seedChain = report.MutantsOf("SeedChain.cs");
            Assert.True(seedChain.Count > 0, $"The report has no mutant of SeedChain.cs (files: {string.Join(", ", report.Files.Keys)}).");

            var tested = seedChain.Where(mutant => !mutant.IsIgnored).ToList();
            Assert.True(tested.Count > 0, $"Every SeedChain.cs mutant is Ignored (statuses: {Statuses(seedChain)}).");

            var uncovered = tested.Where(mutant => !mutant.IsCovered).ToList();
            Assert.True(
                uncovered.Count == 0,
                $"{uncovered.Count} of {tested.Count} non-ignored SeedChain.cs mutants are not covered by any test "
                + $"(statuses: {Statuses(seedChain)}; first: {string.Join("; ", uncovered.Take(5).Select(Describe))}). {Tail(stryker)}");
            Assert.Contains(tested, mutant => mutant.IsDetected);

            var files = report.Files.Keys.Where(file => !file.Replace('\\', '/').Contains("/Fairness.Domain/", StringComparison.Ordinal)).ToList();
            Assert.True(files.Count == 0, $"Stryker mutated files outside Fairness.Domain: {string.Join(", ", files)}");

            Assert.True(
                report.Score >= StrykerConfig.MinimumScore,
                string.Create(CultureInfo.InvariantCulture, $"Fairness.Domain mutation score {report.Score:F2}% < {StrykerConfig.MinimumScore}% ")
                + $"(statuses: {Statuses([.. report.AllMutants])}).");
            Assert.True(stryker.Succeeded, $"Stryker failed although the score passed. {stryker.Describe()}");
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static string Statuses(IReadOnlyList<MutationReport.Mutant> mutants) =>
        string.Join(", ", mutants.GroupBy(mutant => mutant.Status, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => string.Create(CultureInfo.InvariantCulture, $"{group.Key}={group.Count()}")));

    private static string Describe(MutationReport.Mutant mutant) =>
        string.Create(CultureInfo.InvariantCulture, $"#{mutant.Id} {mutant.MutatorName} line {mutant.Line} {mutant.Status}");

    private static string Tail(ProcessResult result) =>
        result.Output.Length <= 1500 ? result.Output : "[...]" + result.Output[^1500..];
}
