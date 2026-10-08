using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Repository.Tests.Integration;

[Collection(RepositoryFixtureDefinition.Name)]
public sealed class FormatVerificationTests(RepositoryCopyFixture repository)
{
    private const string BadFileName = "BadlyFormattedFixture.cs";

    private const string BadlyFormattedSource = """
        namespace   Apogeu.Fixtures   ;

        internal class   BadlyFormattedFixture
        {
                public   int   Value   =   1  ;
              public int Other=2;
        }
        """;

    [Fact(DisplayName = "Dado um arquivo C# fora do formato, quando o dotnet format --verify-no-changes roda, então sai com código diferente de zero")]
    [Trait("Spec", "SPEC-0002:IT-02")]
    public async Task GivenMisformattedCSharpFileWhenFormatVerifyRunsThenExitCodeIsNonZero()
    {
        // Control: the repository itself must be formatted, otherwise a failure below proves nothing.
        var baseline = await RunFormatVerifyAsync();
        Assert.True(
            baseline.Succeeded,
            "Expected `dotnet format Apogeu.slnx --verify-no-changes` to pass on the clean copy. " + baseline.Describe());

        var projectDirectory = FindProjectDirectory();
        var badFile = Path.Combine(projectDirectory, BadFileName);
        File.WriteAllText(badFile, BadlyFormattedSource.ReplaceLineEndings("\n"));
        try
        {
            var result = await RunFormatVerifyAsync();

            Assert.False(
                result.Succeeded,
                "Expected `dotnet format --verify-no-changes` to fail for a misformatted file. " + result.Describe());
            Assert.Contains(BadFileName, result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(badFile);
        }
    }

    private Task<ProcessResult> RunFormatVerifyAsync() =>
        ProcessRunner.DotnetAsync(
            repository.SharedCopyPath,
            ["format", "Apogeu.slnx", "--verify-no-changes"],
            TestContext.Current.CancellationToken);

    private string FindProjectDirectory()
    {
        var buildingBlocks = Path.Combine(repository.SharedCopyPath, "src", "backend", "BuildingBlocks");
        var project = Directory.Exists(buildingBlocks)
            ? Directory.EnumerateFiles(buildingBlocks, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault()
            : null;
        if (project is null)
        {
            Assert.Fail("Expected a .NET project in src/backend/BuildingBlocks (SPEC-0002 §2) to host the misformatted file.");
        }

        return Path.GetDirectoryName(project)!;
    }
}
