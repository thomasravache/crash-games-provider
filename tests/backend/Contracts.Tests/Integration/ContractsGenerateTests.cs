using System.Diagnostics;
using System.Text.Json;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Contracts.Tests.Integration;

[Collection(RepositoryFixtureDefinition.Name)]
public sealed class ContractsGenerateTests(RepositoryCopyFixture repository)
{
    private static readonly TimeSpan GenerateBudget = TimeSpan.FromSeconds(60); // SPEC-0006 §5

    [Fact(DisplayName = "Dado o backend compilado, quando contracts:generate roda, então gera os arquivos REST e de hubs e o typecheck do front passa")]
    [Trait("Spec", "SPEC-0006:IT-01")]
    public async Task GivenCompiledBackendWhenContractsGenerateRunsThenFilesAreGeneratedAndFrontendTypechecks()
    {
        var copy = repository.SharedCopyPath;
        var cancellationToken = TestContext.Current.CancellationToken;

        var install = await repository.EnsureFrontendInstalledAsync();
        Assert.True(install.Succeeded, "Expected `pnpm -C src/frontend install --frozen-lockfile` to succeed. " + install.Describe());

        var build = await ProcessRunner.DotnetAsync(copy, ["build", "Apogeu.slnx"], cancellationToken);
        Assert.True(build.Succeeded, "Expected `dotnet build Apogeu.slnx` to succeed. " + build.Describe());

        var stopwatch = Stopwatch.StartNew();
        var generate = await ProcessRunner.PnpmAsync(copy, ["-C", "src/frontend", "run", "contracts:generate"], cancellationToken);
        stopwatch.Stop();
        Assert.True(generate.Succeeded, "Expected `pnpm -C src/frontend contracts:generate` to succeed. " + generate.Describe());
        Assert.True(
            stopwatch.Elapsed <= GenerateBudget,
            $"`contracts:generate` took {stopwatch.Elapsed.TotalSeconds:F1} s; the budget is {GenerateBudget.TotalSeconds:F0} s (SPEC-0006 §5).");

        var openApiPath = Path.Combine(copy, "artifacts", "openapi", "v1.json");
        Assert.True(File.Exists(openApiPath), "Expected the OpenAPI document at artifacts/openapi/v1.json (SPEC-0006 §2).");
        using (var openApi = JsonDocument.Parse(await File.ReadAllTextAsync(openApiPath, cancellationToken)))
        {
            Assert.True(
                openApi.RootElement.TryGetProperty("paths", out var paths) && paths.TryGetProperty("/api/v1/meta", out _),
                "Expected artifacts/openapi/v1.json to describe GET /api/v1/meta (SPEC-0006 §6).");
        }

        var contractsSource = Path.Combine(copy, "src", "frontend", "packages", "contracts", "src");
        foreach (var area in (string[])["rest", "hubs"])
        {
            var directory = Path.Combine(contractsSource, area);
            var files = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.ts", SearchOption.AllDirectories).ToArray()
                : [];
            Assert.True(files.Length > 0, $"Expected generated TypeScript files in packages/contracts/src/{area} (SPEC-0006 §6).");
        }

        var rest = string.Concat(Directory
            .EnumerateFiles(Path.Combine(contractsSource, "rest"), "*.ts", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
        Assert.Contains("/api/v1/meta", rest, StringComparison.Ordinal);

        var typecheck = await ProcessRunner.PnpmAsync(copy, ["-C", "src/frontend", "typecheck"], cancellationToken);
        Assert.True(typecheck.Succeeded, "Expected `pnpm -C src/frontend typecheck` to pass over the generated contracts. " + typecheck.Describe());
    }
}
