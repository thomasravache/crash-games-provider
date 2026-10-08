using System.Text.RegularExpressions;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Contracts.Tests.Integration;

[Collection(RepositoryFixtureDefinition.Name)]
public sealed class ContractsCheckTests(RepositoryCopyFixture repository)
{
    private const string MetaFeatureDirectory = "src/backend/Modules/Backoffice/Backoffice.Features/Meta";
    private const string ProbeProperty = "public string? DriftProbe { get; init; }";

    private static readonly string[] Identity =
    [
        "-c", "user.name=Contracts Tests",
        "-c", "user.email=contracts-tests@example.invalid",
        "-c", "commit.gpgsign=false",
    ];

    [Fact(DisplayName = "Dado um campo novo num DTO sem regenerar, quando contracts:check roda, então sai com código 1 e mostra o diff")]
    [Trait("Spec", "SPEC-0006:IT-02")]
    public async Task GivenNewDtoFieldWithoutRegeneratingWhenContractsCheckRunsThenItExitsWithOneAndShowsTheDiff()
    {
        var copy = await CreateCommittedCopyAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var install = await ProcessRunner.PnpmAsync(copy, ["-C", "src/frontend", "install", "--frozen-lockfile"], cancellationToken);
        Assert.True(install.Succeeded, "Expected `pnpm -C src/frontend install --frozen-lockfile` to succeed. " + install.Describe());

        // Control: on the committed state the generated contracts are current, otherwise exit 1 below proves nothing.
        var baseline = await BuildAndCheckAsync(copy);
        Assert.True(baseline.ExitCode == 0 && !baseline.TimedOut, "Expected `contracts:check` to exit 0 on the committed state. " + baseline.Describe());

        AddFieldToMetaResponse(copy);
        var result = await BuildAndCheckAsync(copy);

        Assert.True(result.ExitCode == 1, "Expected `contracts:check` to exit with code 1 after a DTO change. " + result.Describe());
        Assert.Matches(new Regex(@"(?m)^\+.*driftProbe", RegexOptions.None, TimeSpan.FromSeconds(1)), result.Output);
    }

    private async Task<string> CreateCommittedCopyAsync()
    {
        var copy = repository.CreateIsolatedCopy();
        var cancellationToken = TestContext.Current.CancellationToken;

        var init = await ProcessRunner.GitAsync(copy, ["init", "--quiet", "--initial-branch=main"], cancellationToken).ConfigureAwait(false);
        Assert.True(init.Succeeded, "Setup failed. " + init.Describe());
        var add = await ProcessRunner.GitAsync(copy, ["add", "--all"], cancellationToken).ConfigureAwait(false);
        Assert.True(add.Succeeded, "Setup failed. " + add.Describe());
        var commit = await ProcessRunner.GitAsync(copy, [.. Identity, "commit", "--quiet", "--no-verify", "--message", "baseline"], cancellationToken).ConfigureAwait(false);
        Assert.True(commit.Succeeded, "Setup failed. " + commit.Describe());

        return copy;
    }

    /// <summary>The CI flow: compile the backend, then compare the committed contracts with a fresh generation.</summary>
    private static async Task<ProcessResult> BuildAndCheckAsync(string copy)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var build = await ProcessRunner.DotnetAsync(copy, ["build", "Apogeu.slnx"], cancellationToken).ConfigureAwait(false);
        Assert.True(build.Succeeded, "Expected `dotnet build Apogeu.slnx` to succeed. " + build.Describe());

        return await ProcessRunner.PnpmAsync(copy, ["-C", "src/frontend", "run", "contracts:check"], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Adds <c>DriftProbe</c> to the GET /api/v1/meta response DTO (the type that declares ApiVersion).</summary>
    private static void AddFieldToMetaResponse(string copy)
    {
        var directory = Path.Combine(copy, MetaFeatureDirectory);
        var candidates = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            : [];

        foreach (var file in candidates)
        {
            var source = File.ReadAllText(file);
            var apiVersion = Regex.Match(source, @"\bApiVersion\b", RegexOptions.None, TimeSpan.FromSeconds(1));
            if (!apiVersion.Success)
            {
                continue;
            }

            var declaration = Regex
                .Matches(source[..apiVersion.Index], @"\b(?:record|class)\s+\w+", RegexOptions.None, TimeSpan.FromSeconds(1))
                .LastOrDefault();
            if (declaration is null)
            {
                continue;
            }

            File.WriteAllText(file, InsertProbe(source, declaration.Index));
            return;
        }

        Assert.Fail($"Expected the GET /api/v1/meta response DTO (a record/class declaring ApiVersion) under {MetaFeatureDirectory} (SPEC-0006 touches).");
    }

    /// <summary>Inserts the probe property into the body of the type declared at <paramref name="start"/> (creating the body for a positional record).</summary>
    private static string InsertProbe(string source, int start)
    {
        var depth = 0;
        for (var index = start; index < source.Length; index++)
        {
            switch (source[index])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ';' when depth == 0:
                    return source[..index] + " { " + ProbeProperty + " }" + source[(index + 1)..];
                case '{' when depth == 0:
                    return source[..(index + 1)] + " " + ProbeProperty + source[(index + 1)..];
            }
        }

        throw new InvalidOperationException("Could not find the end of the Meta response DTO declaration.");
    }
}
