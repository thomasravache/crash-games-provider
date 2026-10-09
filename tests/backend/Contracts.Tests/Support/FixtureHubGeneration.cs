using System.Text.RegularExpressions;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>
/// Runs, once per test class, the hub-contract pipeline (ADR-0008, SPEC-0006 §2) over the test assembly, which holds
/// <see cref="FixtureHub"/>:
/// <list type="number">
/// <item><c>dotnet Apogeu.HubContracts.dll --assembly &lt;assembly.dll&gt; --output &lt;manifest.json&gt;</c> exports the
/// hubs of that assembly only (without <c>--assembly</c> it keeps scanning the Apogeu assemblies next to the tool);</item>
/// <item><c>node tools/contracts/hubs-to-ts.mjs --input &lt;manifest.json&gt; --output &lt;file.ts&gt; --scalars-from &lt;module&gt;</c>
/// turns the manifest into TypeScript (same CLI shape as <c>openapi-to-ts.mjs</c>).</item>
/// </list>
/// </summary>
public sealed partial class FixtureHubGeneration : IAsyncLifetime
{
    public const string HubsScriptPath = "tools/contracts/hubs-to-ts.mjs";

    public const string ScalarsFrom = "../scalars";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "apogeu-hub-contracts-fixture-" + Guid.NewGuid().ToString("N"));

    private string? _typeScript;

    internal string ManifestPath => Path.Combine(_directory, "hubs.json");

    internal string TypeScriptPath => Path.Combine(_directory, "hubs.ts");

    /// <summary>
    /// <c>tools/contracts/HubContracts/bin/&lt;Configuration&gt;/&lt;TFM&gt;/Apogeu.HubContracts.dll</c>, built with the same
    /// configuration as this test assembly (the test project references HubContracts for build order only).
    /// </summary>
    internal static string HubExporterPath
    {
        get
        {
            var output = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
            var configuration = output.Parent?.Name ?? "Debug";
            return RepositoryRoot.Combine("tools", "contracts", "HubContracts", "bin", configuration, output.Name, "Apogeu.HubContracts.dll");
        }
    }

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must not fail the run.
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>TypeScript of the hubs of the test assembly (<see cref="FixtureHub"/>), generated once.</summary>
    internal async Task<string> TypeScriptAsync()
    {
        if (_typeScript is not null)
        {
            return _typeScript;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.True(File.Exists(HubExporterPath), $"Expected the hub exporter at {HubExporterPath}; build the solution first.");

        var testAssembly = typeof(FixtureHub).Assembly.Location;
        var export = await ProcessRunner.RunAsync(
            "dotnet",
            [HubExporterPath, "--assembly", testAssembly, "--output", ManifestPath],
            RepositoryRoot.Path,
            ProcessRunner.ShortTimeout,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        Assert.True(
            export.Succeeded,
            "Expected `dotnet Apogeu.HubContracts.dll --assembly <assembly.dll> --output <manifest.json>` to export the hubs of the given assembly. "
            + export.Describe());
        Assert.True(File.Exists(ManifestPath), $"Expected the hub exporter to write {ManifestPath}.");

        var generate = await ProcessRunner.RunAsync(
            "node",
            [HubsScriptPath, "--input", ManifestPath, "--output", TypeScriptPath, "--scalars-from", ScalarsFrom],
            RepositoryRoot.Path,
            ProcessRunner.ShortTimeout,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        Assert.True(
            generate.Succeeded,
            $"Expected `node {HubsScriptPath} --input <manifest.json> --output <file.ts> --scalars-from <module>` to succeed. " + generate.Describe());
        Assert.True(
            File.Exists(TypeScriptPath),
            $"Expected `node {HubsScriptPath} --input <manifest.json> --output <file.ts>` to write {TypeScriptPath}. "
            + $"Manifest:{Environment.NewLine}{await File.ReadAllTextAsync(ManifestPath, cancellationToken).ConfigureAwait(false)}");

        _typeScript = await File.ReadAllTextAsync(TypeScriptPath, cancellationToken).ConfigureAwait(false);
        return _typeScript;
    }

    /// <summary>Members of <c>export interface <paramref name="name"/> { ... }</c>, one normalized signature per entry.</summary>
    internal static IReadOnlyList<string> InterfaceMembers(string typeScript, string name)
    {
        var match = Regex.Match(
            typeScript,
            $@"export\s+interface\s+{Regex.Escape(name)}\s*\{{(?<body>[^}}]*)\}}",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));
        if (!match.Success)
        {
            Assert.Fail($"The generated TypeScript declares no interface '{name}'. Output:{Environment.NewLine}{typeScript}");
        }

        return [.. match.Groups["body"].Value
            .Split(';')
            .Select(member => Whitespace().Replace(member, " ").Trim())
            .Where(member => member.Length > 0)];
    }

    /// <summary>Signature of <paramref name="method"/> in interface <paramref name="name"/> (<c>Name(args): Result</c>).</summary>
    internal static string Member(string typeScript, string name, string method)
    {
        var members = InterfaceMembers(typeScript, name);
        var member = members.FirstOrDefault(m => m.StartsWith(method + "(", StringComparison.Ordinal));
        if (member is null)
        {
            Assert.Fail($"Interface '{name}' has no method '{method}'. Members: {string.Join(" | ", members)}");
        }

        return member;
    }

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Whitespace();
}
