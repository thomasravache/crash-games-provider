using Xunit;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>
/// Generates, once per test class, the OpenAPI document of the Host with the fixture endpoint
/// (<see cref="FixtureEndpointStartupFilter"/>) and the TypeScript the contracts generator produces from it.
/// </summary>
public sealed class FixtureContractGeneration : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "apogeu-contracts-fixture-" + Guid.NewGuid().ToString("N"));

    internal string OpenApiPath => Path.Combine(_directory, "openapi.json");

    internal string TypeScriptPath => Path.Combine(_directory, "fixture.ts");

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

    /// <summary>OpenAPI document served by the Host in Development with the fixture endpoint mapped.</summary>
    internal async Task<OpenApiDocument> OpenApiAsync()
    {
        using var factory = ContractHost.Create("Development", withFixtureEndpoint: true);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(ContractHost.OpenApiDocumentPath, UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected GET {ContractHost.OpenApiDocumentPath} to succeed in Development; got {(int)response.StatusCode}.");

        await File.WriteAllTextAsync(OpenApiPath, body, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return OpenApiDocument.Parse(body);
    }

    /// <summary>TypeScript generated from <see cref="OpenApiAsync"/>'s document by the contracts generator.</summary>
    internal async Task<string> TypeScriptAsync()
    {
        await OpenApiAsync().ConfigureAwait(false);
        var result = await TypeScriptGenerator.RunAsync(OpenApiPath, TypeScriptPath, TestContext.Current.CancellationToken).ConfigureAwait(false);
        Assert.True(result.Succeeded, $"Expected the REST TypeScript generator ({TypeScriptGenerator.ScriptPath}) to succeed. " + result.Describe());
        Assert.True(File.Exists(TypeScriptPath), $"Expected the generator to write {TypeScriptPath}.");
        return await File.ReadAllTextAsync(TypeScriptPath, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}
