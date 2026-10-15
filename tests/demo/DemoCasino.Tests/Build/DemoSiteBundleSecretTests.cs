using System.Security.Cryptography;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Build;

// SPEC-0043 UT-02 (§5 Segurança, ADR-0027): the production build of the demo-site never carries the HMAC secret of the
// demo operator. The build runs with a sentinel secret in every environment variable it could plausibly be read from
// (the AppHost parameter names of the DemoCasino and Vite's VITE_ prefix); then every emitted file is searched for it.
// Control: the inspected bundle must be the real entry screen, i.e. it calls the casino BFF (/casino/session);
// otherwise an empty bundle would pass for the wrong reason.
// Requires the front-end installed (pnpm -C src/frontend install --frozen-lockfile), as the other pnpm tests.
public sealed class DemoSiteBundleSecretTests
{
    private const string BffSessionPath = "/casino/session";

    [Fact(DisplayName = "Dado o build de produção do demo-site, quando os arquivos são inspecionados, então o segredo do operador não aparece")]
    [Trait("Spec", "SPEC-0043:UT-02")]
    public async Task GivenDemoSiteProductionBuildWhenFilesAreInspectedThenOperatorSecretIsAbsent()
    {
        var ct = TestContext.Current.CancellationToken;
        var frontend = RepositoryRoot.Combine("src", "frontend");
        Assert.True(
            Directory.Exists(Path.Combine(frontend, "node_modules")),
            "src/frontend is not installed: run `pnpm -C src/frontend install --frozen-lockfile` first.");

        var sentinel = "sentinela-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        var outDir = Path.Combine(Path.GetTempPath(), "apogeu-demo-site-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6)));
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NODE_ENV"] = "production",
            ["Apogeu__OperatorSecret"] = sentinel,
            ["APOGEU__OPERATORSECRET"] = sentinel,
            ["APOGEU_OPERATOR_SECRET"] = sentinel,
            ["OPERATOR_SECRET"] = sentinel,
            ["VITE_APOGEU_OPERATOR_SECRET"] = sentinel,
            ["VITE_OPERATOR_SECRET"] = sentinel,
        };

        try
        {
            var build = await ProcessRunner.RunAsync(
                "pnpm",
                ["--filter", "@apogeu/demo-site", "exec", "vite", "build", "--mode", "production", "--outDir", outDir, "--emptyOutDir"],
                frontend,
                ProcessRunner.LongTimeout,
                environment,
                ct);
            Assert.True(build.Succeeded, "Expected the demo-site production build to succeed. " + build.Describe());

            var files = Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories).ToList();
            Assert.NotEmpty(files);

            var contents = files.ToDictionary(file => Path.GetRelativePath(outDir, file), File.ReadAllText, StringComparer.Ordinal);
            Assert.True(
                contents.Values.Any(text => text.Contains(BffSessionPath, StringComparison.Ordinal)),
                $"The demo-site bundle must be the entry screen that calls {BffSessionPath} (control of this test); it is not.");

            var leaking = contents.Where(file => file.Value.Contains(sentinel, StringComparison.Ordinal)).Select(file => file.Key).ToList();
            Assert.True(leaking.Count == 0, "The operator secret reached the demo-site bundle in: " + string.Join(", ", leaking));
        }
        finally
        {
            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, recursive: true);
            }
        }
    }
}
