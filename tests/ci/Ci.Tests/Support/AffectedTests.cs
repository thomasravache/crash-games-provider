using Apogeu.Repository.Tests.Support;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Runs tools/ci/affected-tests (SPEC-0051) with <c>python3</c>: against the real repository for the target mapping
/// (<c>--files … --list</c>) and against a temporary git repository (<see cref="AffectedTestsRepository"/>) for the
/// git-based selection.
/// </summary>
public static class AffectedTests
{
    public const string Script = "tools/ci/affected-tests";

    public const string Solution = "solution";
    public const string Frontend = "frontend";
    public const string ArchitectureTests = "tests/backend/Architecture.Tests/Architecture.Tests.csproj";
    public const string BettingTests = "tests/backend/Modules/Betting.Tests/Betting.Tests.csproj";
    public const string RoundsTests = "tests/backend/Modules/Rounds.Tests/Rounds.Tests.csproj";
    public const string WalletTests = "tests/backend/Modules/Wallet.Tests/Wallet.Tests.csproj";
    public const string CiTests = "tests/ci/Ci.Tests/Ci.Tests.csproj";

    /// <summary>Runs <c>affected-tests --files &lt;files&gt; --list</c> from the repository root.</summary>
    public static Task<ProcessResult> ListAsync(IReadOnlyCollection<string> files, CancellationToken cancellationToken) =>
        RunAsync(RepositoryRoot.Path, RepositoryRoot.Combine("tools", "ci", "affected-tests"), ["--files", .. files, "--list"], cancellationToken);

    /// <summary>Runs the script at <paramref name="script"/> with <paramref name="workingDirectory"/> as the current directory.</summary>
    public static Task<ProcessResult> RunAsync(
        string workingDirectory,
        string script,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        // An inherited AFFECTED_BASE must not change the base the tests choose.
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AFFECTED_BASE"] = string.Empty };
        return ProcessRunner.RunAsync("python3", [script, .. arguments], workingDirectory, ProcessRunner.ShortTimeout, environment, cancellationToken);
    }

    /// <summary>The non-empty, non-comment lines printed by the script, sorted ordinally.</summary>
    public static IReadOnlyList<string> Lines(ProcessResult result) =>
    [
        .. result.StandardOutput
            .Split('\n')
            .Select(line => line.TrimEnd('\r').Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>The two <c>dotnet test</c> commands of the test mode for <paramref name="target"/> (project or solution).</summary>
    public static IEnumerable<string> TestCommands(string target) =>
    [
        $"dotnet test {target} --filter \"Category!=Nightly&Category!=CiLive&Category!=Performance\"",
        $"dotnet test {target} --filter Category=Performance -m:1",
    ];
}
