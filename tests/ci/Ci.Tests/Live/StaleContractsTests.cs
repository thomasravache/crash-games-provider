using System.Text.Json;
using System.Text.RegularExpressions;
using Apogeu.Ci.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Live;

/// <summary>IT-04 and CT-01 (live part): the contracts job fails when packages/contracts diverges from the backend.</summary>
[Collection(CiLiveDefinition.Name)]
public sealed partial class StaleContractsTests
{
    private const string ContractsSources = "src/frontend/packages/contracts/src/rest";
    private const string FrontendManifest = "src/frontend/package.json";
    private const string ContractsCheckScript = "contracts:check";
    private const string ProbeField = "CiLiveProbe";
    private const string HandEditMarker = "ci-live probe CT-01: hand edit, out of sync with contracts:generate";

    [Fact(
        DisplayName = "Dado um PR que altera um DTO sem regenerar packages/contracts, quando o CI roda, então o job contracts falha",
        Skip = CiLive.SkipReason,
        SkipUnless = nameof(CiLive.Enabled),
        SkipType = typeof(CiLive))]
    [Trait("Spec", "SPEC-0004:IT-04")]
    [Trait("Category", CiLive.Category)]
    public async Task GivenPullRequestChangingDtoWithoutRegeneratingWhenCiRunsThenContractsJobFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CiLive.EnsureReadyAsync(CiPipeline.CiWorkflow, [CiPipeline.Contracts], cancellationToken);

        await using var repository = await ProbeRepository.CloneAsync("IT-04", cancellationToken);
        EnsureContractsCheckScript(repository);
        AddFieldToMetaDto(repository);
        await repository.CommitAsync(allowEmpty: false, cancellationToken);
        await using var pullRequest = await ProbePullRequest.OpenAsync(repository, cancellationToken);

        var checks = await pullRequest.WaitForChecksAsync(CiPipeline.CiWorkflowName, [CiPipeline.Contracts], cancellationToken);

        LiveAssert.Bucket(checks, CiPipeline.Contracts, "fail");
        // The diff printed by contracts:check adds the probe field (camelCase in OpenAPI and TypeScript), and the script
        // ends with its drift message; a build broken by the DTO edit or a missing script prints neither.
        await LiveAssert.FailedOutputMatchesAsync(
            checks, CiPipeline.Contracts, cancellationToken, AddedProbeFieldLine(), DriftMessage());
        TestContext.Current.TestOutputHelper?.WriteLine($"IT-04 evidence: {pullRequest.Url}");
    }

    [Fact(
        DisplayName = "Dados contratos desatualizados em packages/contracts, quando o job contracts executa contracts:check, então recebe exit ≠ 0 e falha",
        Skip = CiLive.SkipReason,
        SkipUnless = nameof(CiLive.Enabled),
        SkipType = typeof(CiLive))]
    [Trait("Spec", "SPEC-0004:CT-01")]
    [Trait("Category", CiLive.Category)]
    public async Task GivenStaleGeneratedContractsWhenContractsJobRunsThenContractsCheckFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CiLive.EnsureReadyAsync(CiPipeline.CiWorkflow, [CiPipeline.Contracts], cancellationToken);

        await using var repository = await ProbeRepository.CloneAsync("CT-01", cancellationToken);
        EnsureContractsCheckScript(repository);
        var generated = Directory.Exists(repository.PathOf(ContractsSources))
            ? Directory.EnumerateFiles(repository.PathOf(ContractsSources), "*.ts", SearchOption.AllDirectories).Order(StringComparer.Ordinal).FirstOrDefault()
            : null;
        Assert.True(generated is not null, $"Setup failed: no generated contract (*.ts) under {ContractsSources} on {CiLive.BaseBranch}.");
        File.AppendAllText(generated, "// " + HandEditMarker + "\n");
        await repository.CommitAsync(allowEmpty: false, cancellationToken);
        await using var pullRequest = await ProbePullRequest.OpenAsync(repository, cancellationToken);

        var checks = await pullRequest.WaitForChecksAsync(CiPipeline.CiWorkflowName, [CiPipeline.Contracts], cancellationToken);

        LiveAssert.Bucket(checks, CiPipeline.Contracts, "fail");
        // The fresh generation lacks the hand-edited line, so the diff removes it; plus the drift message of the script.
        await LiveAssert.FailedOutputMatchesAsync(
            checks, CiPipeline.Contracts, cancellationToken, RemovedHandEditLine(), DriftMessage());
        TestContext.Current.TestOutputHelper?.WriteLine($"CT-01 evidence: {pullRequest.Url}");
    }

    /// <summary>
    /// Precondition: main declares the <c>contracts:check</c> script (SPEC-0006) in src/frontend/package.json; without
    /// it the job would fail for a reason other than drift. Checked on the clone, before anything is pushed.
    /// </summary>
    private static void EnsureContractsCheckScript(ProbeRepository repository)
    {
        var manifest = repository.PathOf(FrontendManifest);
        Assert.True(File.Exists(manifest), $"Precondition failed: {FrontendManifest} not found on {CiLive.BaseBranch}.");
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var declared = document.RootElement.TryGetProperty("scripts", out var scripts)
            && scripts.ValueKind == JsonValueKind.Object
            && scripts.TryGetProperty(ContractsCheckScript, out var script)
            && script.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(script.GetString());
        Assert.True(declared, $"Precondition failed: {FrontendManifest} on {CiLive.BaseBranch} has no '{ContractsCheckScript}' script (SPEC-0006).");
    }

    /// <summary>
    /// Adds an optional field to the DTO of GET /api/v1/meta (SPEC-0006@1: apiVersion, serverTime) without touching
    /// packages/contracts. Positional records get a trailing optional parameter; classes get an init property.
    /// </summary>
    private static void AddFieldToMetaDto(ProbeRepository repository)
    {
        var sources = Directory
            .EnumerateFiles(repository.PathOf("src/backend"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains("/bin/", StringComparison.Ordinal) && !file.Contains("/obj/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        foreach (var file in sources)
        {
            var text = File.ReadAllText(file);
            var record = PositionalRecordWithApiVersion().Match(text);
            if (record.Success)
            {
                var parameters = record.Groups["parameters"];
                File.WriteAllText(file, text.Insert(parameters.Index + parameters.Length, ", string? " + ProbeField + " = null"));
                return;
            }

            var property = ApiVersionProperty().Match(text);
            if (property.Success)
            {
                var indent = property.Groups["indent"].Value;
                File.WriteAllText(
                    file,
                    text.Insert(property.Index + property.Length, "\n" + indent + "public string? " + ProbeField + " { get; init; }"));
                return;
            }
        }

        Assert.Fail("Setup failed: the DTO of GET /api/v1/meta (a record or class with ApiVersion) was not found under src/backend.");
    }

    [GeneratedRegex(@"\brecord\s+(?:class\s+|struct\s+)?\w+\s*\((?<parameters>[^()]*\bApiVersion\b[^()]*)\)", RegexOptions.None, 1000)]
    private static partial Regex PositionalRecordWithApiVersion();

    [GeneratedRegex(@"^(?<indent>[ \t]*)public\s+[\w?<>.]+\s+ApiVersion\s*\{[^}\n]*\}[^\n]*$", RegexOptions.Multiline, 1000)]
    private static partial Regex ApiVersionProperty();

    /// <summary>A line added by the diff of contracts:check that names the probe field (<c>ciLiveProbe</c>).</summary>
    [GeneratedRegex(@"^\+(?!\+\+).*\bciLiveProbe\b", RegexOptions.Multiline, 1000)]
    private static partial Regex AddedProbeFieldLine();

    /// <summary>A line removed by the diff of contracts:check: the hand edit that the generator does not produce.</summary>
    [GeneratedRegex(@"^-(?!--).*ci-live probe CT-01: hand edit", RegexOptions.Multiline, 1000)]
    private static partial Regex RemovedHandEditLine();

    /// <summary>Message printed by tools/contracts/contracts.mjs check on drift (SPEC-0006).</summary>
    [GeneratedRegex(@"The committed contracts differ from the backend", RegexOptions.None, 1000)]
    private static partial Regex DriftMessage();
}
