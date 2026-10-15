using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Unit;

/// <summary>UT-01 (SPEC-0060): a change only in the demo casino triggers the .NET jobs of ci.yml.</summary>
public sealed class DemoCasinoChangeFilterTests
{
    private const string ChangesJob = "changes";

    public static TheoryData<string, string[]> DemoChanges => new()
    {
        { "src/demo/DemoCasino/Program.cs", [CiPipeline.Backend, CiPipeline.E2e, CiPipeline.Contracts] },
        { "tests/demo/DemoCasino.Tests/x.cs", [CiPipeline.Backend] },
    };

    [Theory(DisplayName = "Dado o ci.yml, quando os filtros do job changes são lidos, então uma mudança só no cassino demo aciona os filtros .NET")]
    [MemberData(nameof(DemoChanges))]
    [Trait("Spec", "SPEC-0060:UT-01")]
    public void GivenCiWorkflowWhenChangesFiltersAreReadThenDemoCasinoChangeTriggersDotnetFilters(string changedFile, string[] expectedFilters)
    {
        var ci = WorkflowFile.TryLoad(CiPipeline.CiWorkflow);
        Assert.True(ci is not null, $"workflow {CiPipeline.CiWorkflow} not found in {WorkflowFile.Directory}");
        var jobs = ci.Jobs();
        Assert.True(
            jobs.TryGetValue(ChangesJob, out var changesJob),
            $"{CiPipeline.CiWorkflow} has no `{ChangesJob}` job (jobs: {string.Join(", ", jobs.Keys)}).");
        var filters = PathsFilter.FromJob(changesJob);
        Assert.True(filters is not null, $"job `{ChangesJob}` of {CiPipeline.CiWorkflow} has no paths-filter `filters: |` block.");

        var missing = expectedFilters.Where(filter => !filters.Triggers(filter, changedFile)).ToList();

        Assert.True(
            missing.Count == 0,
            $"a change only in `{changedFile}` does not trigger the filter(s) {string.Join(", ", missing.Select(f => $"`{f}`"))} "
            + $"of job `{ChangesJob}` in {CiPipeline.CiWorkflow} (globs: "
            + string.Join("; ", missing.Select(f => $"{f} = [{string.Join(", ", filters.Globs(f))}]")) + ").");
    }

    [Fact(DisplayName = "Dado o leitor dos filtros, quando o ci.yml é lido, então as âncoras são resolvidas e os globs seguem o picomatch")]
    [Trait("Spec", "SPEC-0060:UT-01")]
    public void GivenFilterReaderWhenCiWorkflowIsReadThenAnchorsResolveAndGlobsMatch()
    {
        var ci = WorkflowFile.TryLoad(CiPipeline.CiWorkflow);
        Assert.True(ci is not null, $"workflow {CiPipeline.CiWorkflow} not found in {WorkflowFile.Directory}");
        var filters = PathsFilter.FromJob(ci.Jobs()[ChangesJob]);
        Assert.True(filters is not null);

        // Sanity of the reader: paths already covered today trigger through the resolved aliases.
        Assert.True(filters.Triggers(CiPipeline.Backend, "src/backend/Wallet/Wallet.Api/Program.cs"));
        Assert.True(filters.Triggers(CiPipeline.Contracts, ".github/workflows/ci.yml"));
        Assert.True(filters.Triggers(CiPipeline.E2e, "src/frontend/apps/game/src/main.ts"));
        Assert.False(filters.Triggers(CiPipeline.Frontend, "src/backend/Wallet/Wallet.Api/Program.cs"));
        Assert.True(PathsFilter.Matches("src/**", "src/a/b/c.cs"));
        Assert.True(PathsFilter.Matches(".config/**", ".config/dotnet-tools.json"));
        Assert.False(PathsFilter.Matches("src/*", "src/a/b.cs"));
        Assert.False(PathsFilter.Matches("src/backend/**", "src/demo/DemoCasino/Program.cs"));
    }
}
