using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Repository.Tests.Integration;

[Collection(RepositoryFixtureDefinition.Name)]
public sealed class SecretsIgnoredTests(RepositoryCopyFixture repository)
{
    private const string ControlFile = "src/backend/Host/appsettings.json";

    [Theory(DisplayName = "Dado um arquivo de segredo local, quando o git status --porcelain roda, então ele não aparece")]
    [Trait("Spec", "SPEC-0002:IT-06")]
    [InlineData(".env")]
    [InlineData("src/backend/Host/appsettings.Development.local.json")]
    [InlineData(".env.local")]
    [InlineData("src/backend/Host/appsettings.Production.local.json")]
    [InlineData("secrets.json")]
    public async Task GivenLocalSecretFileWhenGitStatusRunsThenItIsNotListed(string secretFile)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workTree = repository.CreateScratchDirectory();

        // Only the repository's own ignore rules are under test.
        var repositoryIgnore = RepositoryRoot.Combine(".gitignore");
        if (File.Exists(repositoryIgnore))
        {
            File.Copy(repositoryIgnore, Path.Combine(workTree, ".gitignore"));
        }

        var init = await ProcessRunner.GitAsync(workTree, ["init", "--quiet", "--initial-branch=main"], cancellationToken);
        Assert.True(init.Succeeded, "Setup failed. " + init.Describe());

        CreateFile(workTree, secretFile, "{ \"secret\": \"not-a-real-secret\" }");
        CreateFile(workTree, ControlFile, "{ }");

        var status = await ProcessRunner.GitAsync(workTree, ["status", "--porcelain", "--untracked-files=all"], cancellationToken);
        Assert.True(status.Succeeded, "Setup failed. " + status.Describe());

        var listed = status.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line[3..].Trim('"'))
            .ToList();

        // Control: a regular settings file is still reported, so the status output is meaningful.
        Assert.Contains(ControlFile, listed, StringComparer.Ordinal);
        Assert.DoesNotContain(secretFile, listed, StringComparer.Ordinal);
    }

    private static void CreateFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
