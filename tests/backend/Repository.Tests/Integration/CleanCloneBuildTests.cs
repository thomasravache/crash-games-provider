using System.Text.RegularExpressions;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Repository.Tests.Integration;

[Collection(RepositoryFixtureDefinition.Name)]
public sealed partial class CleanCloneBuildTests(RepositoryCopyFixture repository)
{
    [Fact(DisplayName = "Dado um clone limpo, quando o dotnet build roda, então termina com sucesso e zero avisos")]
    [Trait("Spec", "SPEC-0002:IT-01")]
    public async Task GivenCleanCloneWhenDotnetBuildRunsThenItSucceedsWithZeroWarnings()
    {
        var build = await ProcessRunner.DotnetAsync(
            repository.SharedCopyPath,
            ["build", "Apogeu.slnx"],
            TestContext.Current.CancellationToken);

        Assert.True(build.Succeeded, "Expected `dotnet build Apogeu.slnx` to succeed. " + build.Describe());
        Assert.Contains("0 Warning(s)", build.Output, StringComparison.Ordinal);
        Assert.Contains("0 Error(s)", build.Output, StringComparison.Ordinal);
        Assert.False(
            CompilerWarning().IsMatch(build.Output),
            "Expected no compiler/analyzer warning in the build output. " + build.Describe());
    }

    [Fact(DisplayName = "Dado um clone limpo, quando o pnpm build roda, então termina com sucesso e zero avisos")]
    [Trait("Spec", "SPEC-0002:IT-01")]
    public async Task GivenCleanCloneWhenPnpmBuildRunsThenItSucceedsWithZeroWarnings()
    {
        var install = await repository.EnsureFrontendInstalledAsync();
        Assert.True(
            install.Succeeded,
            "Expected `pnpm -C src/frontend install --frozen-lockfile` to succeed. " + install.Describe());

        var build = await ProcessRunner.PnpmAsync(
            repository.SharedCopyPath,
            ["-C", "src/frontend", "build"],
            TestContext.Current.CancellationToken);

        Assert.True(build.Succeeded, "Expected `pnpm -C src/frontend build` to succeed. " + build.Describe());
        Assert.False(
            WarningLine().IsMatch(build.Output),
            "Expected no warning in the `pnpm -C src/frontend build` output. " + build.Describe());
    }

    [GeneratedRegex(@"\bwarning\s+[A-Za-z]+\d+\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex CompilerWarning();

    [GeneratedRegex(@"\bwarn(ing)?\b|\(!\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex WarningLine();
}
