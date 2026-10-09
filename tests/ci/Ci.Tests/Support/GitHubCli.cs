using System.Text.Json;
using Apogeu.Repository.Tests.Support;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Runs the GitHub CLI against <see cref="CiLive.Repository"/>. Authentication comes from GH_TOKEN in the environment
/// (inherited by the child process); the token is never passed on the command line nor written anywhere.
/// </summary>
public static class GitHubCli
{
    private static readonly Dictionary<string, string> Environment = new(StringComparer.Ordinal)
    {
        ["GH_PROMPT_DISABLED"] = "1",
        ["GH_NO_UPDATE_NOTIFIER"] = "1",
        ["GH_REPO"] = CiLive.Repository,
    };

    public static Task<ProcessResult> RunAsync(IEnumerable<string> arguments, CancellationToken cancellationToken) =>
        RunAsync(arguments, ProcessRunner.ShortTimeout, cancellationToken);

    /// <summary>Same as <see cref="RunAsync(IEnumerable{string}, CancellationToken)"/> for slow commands (e.g. artifact downloads).</summary>
    public static Task<ProcessResult> RunAsync(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken) =>
        ProcessRunner.RunAsync("gh", arguments, System.IO.Path.GetTempPath(), timeout, Environment, cancellationToken);

    /// <summary>Runs a gh command that prints JSON and parses it; fails the test when the command fails.</summary>
    public static async Task<JsonElement> JsonAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var result = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("gh failed. " + result.Describe());
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
        return document.RootElement.Clone();
    }
}
