using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Repository.Tests.Integration;

[Collection(RepositoryFixtureDefinition.Name)]
public sealed class CompilerWarningsTests(RepositoryCopyFixture repository)
{
    private const string ProjectSource = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <OutputType>Library</OutputType>
          </PropertyGroup>
        </Project>
        """;

    private const string ClassSource = """
        namespace Apogeu.Fixtures;

        public static class UnusedVariableFixture
        {
            public static void Run()
            {
                int unused;
            }
        }
        """;

    [Fact(DisplayName = "Dado um projeto temporário com variável não usada, quando compilado, então o build falha")]
    [Trait("Spec", "SPEC-0002:IT-03")]
    public async Task GivenProjectWithUnusedVariableWhenBuiltThenBuildFailsWithTheWarningAsError()
    {
        // Sibling of the real projects, so it inherits Directory.Build.props but is not globbed into any of them.
        var projectDirectory = Path.Combine(repository.SharedCopyPath, "src", "backend", "Fixtures.Temp", "UnusedVariable");
        Directory.CreateDirectory(projectDirectory);
        try
        {
            var projectFile = Path.Combine(projectDirectory, "UnusedVariable.csproj");
            File.WriteAllText(projectFile, ProjectSource.ReplaceLineEndings("\n"));
            File.WriteAllText(Path.Combine(projectDirectory, "UnusedVariableFixture.cs"), ClassSource.ReplaceLineEndings("\n"));

            var result = await ProcessRunner.DotnetAsync(
                repository.SharedCopyPath,
                ["build", Path.Combine("src", "backend", "Fixtures.Temp", "UnusedVariable", "UnusedVariable.csproj")],
                TestContext.Current.CancellationToken);

            Assert.False(
                result.Succeeded,
                "Expected the build to fail because warnings are errors. " + result.Describe());
            Assert.Contains("error CS0168", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(Path.Combine(repository.SharedCopyPath, "src", "backend", "Fixtures.Temp"), recursive: true);
        }
    }
}
