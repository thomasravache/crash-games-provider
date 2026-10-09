using System.Globalization;
using System.Text.RegularExpressions;
using Apogeu.Ci.Tests.Support;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Unit;

/// <summary>UT-01: the workflows are valid (actionlint), pin every action by commit SHA and declare minimal permissions.</summary>
public sealed partial class WorkflowSecurityTests
{
    private const string ValidWorkflow = """
        name: fixture
        on:
          pull_request:
            branches: [main]
        permissions:
          contents: read
        jobs:
          build:
            runs-on: ubuntu-latest
            steps:
              - uses: actions/checkout@0123456789abcdef0123456789abcdef01234567
              - run: echo ok
        """;

    [Fact(DisplayName = "Dado o contrato da SPEC-0004, quando .github/workflows é lido, então ci.yml, images.yml e nightly.yml existem")]
    [Trait("Spec", "SPEC-0004:UT-01")]
    public void GivenContractWhenWorkflowsDirectoryIsReadThenContractWorkflowsExist()
    {
        foreach (var workflow in CiPipeline.ContractWorkflows)
        {
            Assert.True(
                WorkflowFile.TryLoad(workflow) is not null,
                $"workflow {workflow} not found in {WorkflowFile.Directory}");
        }
    }

    [Fact(DisplayName = "Dados os workflows, quando o actionlint fixado roda, então não há erros")]
    [Trait("Spec", "SPEC-0004:UT-01")]
    public async Task GivenWorkflowsWhenActionlintRunsThenThereAreNoErrors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        AssertWorkflowsPresent();

        var result = await Actionlint.RunAsync(RepositoryRoot.Path, cancellationToken);

        Assert.True(result.Succeeded, "actionlint reported errors. " + result.Describe());
    }

    [Fact(DisplayName = "Dados os workflows, quando os `uses:` são lidos, então toda ação externa está fixada por SHA completo")]
    [Trait("Spec", "SPEC-0004:UT-01")]
    public void GivenWorkflowsWhenUsesAreReadThenEveryExternalActionIsPinnedBySha()
    {
        var workflows = AssertWorkflowsPresent();

        var unpinned = workflows
            .SelectMany(workflow => workflow.UsesReferences().Select(use => (workflow.Name, use.Line, use.Reference)))
            .Where(use => !IsPinned(use.Reference))
            .Select(use => string.Create(CultureInfo.InvariantCulture, $"{use.Name}:{use.Line} uses {use.Reference}"))
            .ToList();

        Assert.True(
            unpinned.Count == 0,
            "Actions not pinned by a full commit SHA (docker:// images need @sha256 digests):" + Environment.NewLine
            + string.Join(Environment.NewLine, unpinned));
    }

    [Fact(DisplayName = "Dados os workflows, quando lidos, então todo workflow declara `permissions` no topo sem escrita")]
    [Trait("Spec", "SPEC-0004:UT-01")]
    public void GivenWorkflowsWhenReadThenEveryWorkflowDeclaresReadOnlyTopLevelPermissions()
    {
        var workflows = AssertWorkflowsPresent();

        foreach (var workflow in workflows)
        {
            var permissions = workflow.TopLevelBlock("permissions");
            Assert.True(permissions is not null, $"{workflow.Name} does not declare a top-level `permissions` block.");
            Assert.False(
                GrantsMoreThanRead().IsMatch(permissions),
                $"{workflow.Name}: the top-level permissions must be read-only (grant write only per job):{Environment.NewLine}{permissions}");
        }
    }

    [Fact(DisplayName = "Dados os workflows, quando tools/ci/check-workflows roda, então sai com código 0")]
    [Trait("Spec", "SPEC-0004:UT-01")]
    public async Task GivenWorkflowsWhenCheckWorkflowsRunsThenItSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        AssertWorkflowsPresent();

        var result = await RunCheckWorkflowsAsync(WorkflowFile.WorkflowsDirectory, cancellationToken);

        Assert.True(result.Succeeded, "check-workflows rejected the repository workflows. " + result.Describe());
    }

    [Theory(DisplayName = "Dado um workflow inseguro, quando tools/ci/check-workflows roda, então sai com código diferente de 0")]
    [Trait("Spec", "SPEC-0004:UT-01")]
    [InlineData("action pinned by tag", "uses: actions/checkout@0123456789abcdef0123456789abcdef01234567", "uses: actions/checkout@v4")]
    [InlineData("action pinned by short SHA", "uses: actions/checkout@0123456789abcdef0123456789abcdef01234567", "uses: actions/checkout@0123456")]
    [InlineData("no permissions", "permissions:\n  contents: read\n", "")]
    public async Task GivenInsecureWorkflowWhenCheckWorkflowsRunsThenItFails(string scenario, string original, string replacement)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var insecure = ValidWorkflow.Replace(original, replacement, StringComparison.Ordinal);
        Assert.NotEqual(ValidWorkflow, insecure, StringComparer.Ordinal);

        using var fixture = WorkflowFixtureDirectory.Create(insecure);
        var result = await RunCheckWorkflowsAsync(fixture.Path, cancellationToken);

        Assert.False(result.TimedOut, result.Describe());
        Assert.True(
            result.ExitCode is not 0 and not -1,
            $"check-workflows accepted an insecure workflow ({scenario}) or could not start. " + result.Describe());
    }

    [Fact(DisplayName = "Dado um workflow seguro, quando tools/ci/check-workflows roda sobre ele, então sai com código 0")]
    [Trait("Spec", "SPEC-0004:UT-01")]
    public async Task GivenSecureWorkflowWhenCheckWorkflowsRunsThenItSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var fixture = WorkflowFixtureDirectory.Create(ValidWorkflow);
        var result = await RunCheckWorkflowsAsync(fixture.Path, cancellationToken);

        Assert.True(result.Succeeded, "check-workflows rejected a secure workflow. " + result.Describe());
    }

    private static IReadOnlyList<WorkflowFile> AssertWorkflowsPresent()
    {
        var workflows = WorkflowFile.LoadAll(WorkflowFile.WorkflowsDirectory);
        Assert.True(
            workflows.Any(workflow => string.Equals(workflow.Name, CiPipeline.CiWorkflow, StringComparison.Ordinal)),
            $"workflow {CiPipeline.CiWorkflow} not found in {WorkflowFile.Directory}");
        return workflows;
    }

    private static bool IsPinned(string reference)
    {
        if (reference.StartsWith("./", StringComparison.Ordinal))
        {
            return true; // local action or reusable workflow of this repository
        }

        return reference.StartsWith("docker://", StringComparison.Ordinal)
            ? DockerDigest().IsMatch(reference)
            : FullShaPin().IsMatch(reference);
    }

    private static Task<ProcessResult> RunCheckWorkflowsAsync(string workflowsDirectory, CancellationToken cancellationToken)
    {
        var script = RepositoryRoot.Combine(CiPipeline.CheckWorkflowsScript);
        Assert.True(File.Exists(script), $"{CiPipeline.CheckWorkflowsScript} not found (expected an executable script).");
        return ProcessRunner.RunAsync(script, [workflowsDirectory], RepositoryRoot.Path, ProcessRunner.ShortTimeout, cancellationToken: cancellationToken);
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+/[A-Za-z0-9_./-]+@[0-9a-f]{40}$", RegexOptions.None, 1000)]
    private static partial Regex FullShaPin();

    [GeneratedRegex("^docker://[^@\\s]+@sha256:[0-9a-f]{64}$", RegexOptions.None, 1000)]
    private static partial Regex DockerDigest();

    [GeneratedRegex(@"\bwrite(-all)?\b|\bread-all\b", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex GrantsMoreThanRead();
}
