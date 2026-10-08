using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Repository.Tests.Integration;

[Collection(RepositoryFixtureDefinition.Name)]
public sealed class FrontendLintTests(RepositoryCopyFixture repository)
{
    private const string FixtureFileName = "LintFixture.tsx";

    private const string FixtureSource = """
        export function LintFixture(props: { value: any }) {
          return <img src="/fixture.png" data-value={props.value} />;
        }
        """;

    [Fact(DisplayName = "Dado um TSX com any explícito e img sem alt, quando o pnpm lint roda, então falha apontando as duas regras")]
    [Trait("Spec", "SPEC-0002:IT-04")]
    public async Task GivenTsxWithExplicitAnyAndImgWithoutAltWhenLintRunsThenItFailsOnBothRules()
    {
        var install = await repository.EnsureFrontendInstalledAsync();
        Assert.True(
            install.Succeeded,
            "Expected `pnpm -C src/frontend install --frozen-lockfile` to succeed. " + install.Describe());

        // Control: lint must be clean before the bad file is added, otherwise a failure proves nothing.
        var baseline = await RunLintAsync();
        Assert.True(baseline.Succeeded, "Expected `pnpm -C src/frontend lint` to pass on the clean copy. " + baseline.Describe());

        var sourceDirectory = Path.Combine(repository.SharedCopyPath, "src", "frontend", "apps", "game-pipa", "src");
        Assert.True(Directory.Exists(sourceDirectory), "Expected src/frontend/apps/game-pipa/src to exist (SPEC-0002 §2).");

        var fixture = Path.Combine(sourceDirectory, FixtureFileName);
        File.WriteAllText(fixture, FixtureSource.ReplaceLineEndings("\n") + "\n");
        try
        {
            var result = await RunLintAsync();

            Assert.False(result.Succeeded, "Expected `pnpm -C src/frontend lint` to fail. " + result.Describe());
            Assert.Contains("@typescript-eslint/no-explicit-any", result.Output, StringComparison.Ordinal);
            Assert.Contains("jsx-a11y/alt-text", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(fixture);
        }
    }

    private Task<ProcessResult> RunLintAsync() =>
        ProcessRunner.PnpmAsync(
            repository.SharedCopyPath,
            ["-C", "src/frontend", "lint"],
            TestContext.Current.CancellationToken);
}
