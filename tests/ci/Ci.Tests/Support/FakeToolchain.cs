using Xunit;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Fake <c>dotnet</c> and <c>pnpm</c> executables (SPEC-0051 IT-02) in a temporary directory, deleted on dispose: each
/// appends <c>&lt;tool&gt; &lt;argv&gt;</c> to a log file and exits with the code in <c>FAKE_DOTNET_EXIT</c> /
/// <c>FAKE_PNPM_EXIT</c> (0 when unset). <see cref="Environment"/> puts them first on <c>PATH</c>, keeping the inherited
/// <c>PATH</c> after them so that git and python3 stay reachable.
/// </summary>
public sealed class FakeToolchain : IDisposable
{
    private FakeToolchain(string path)
    {
        Path = path;
        BinDirectory = System.IO.Path.Combine(path, "bin");
        LogFile = System.IO.Path.Combine(path, "calls.log");
    }

    public string Path { get; }

    public string BinDirectory { get; }

    public string LogFile { get; }

    public static FakeToolchain Create()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The fake toolchain uses POSIX shell scripts.");
        }

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "apogeu-ci-fake-tools-" + Guid.NewGuid().ToString("N"));
        var toolchain = new FakeToolchain(path);
        try
        {
            Directory.CreateDirectory(toolchain.BinDirectory);
            toolchain.WriteTool("dotnet", "FAKE_DOTNET_EXIT");
            toolchain.WriteTool("pnpm", "FAKE_PNPM_EXIT");
            return toolchain;
        }
        catch
        {
            toolchain.Dispose();
            throw;
        }
    }

    /// <summary>The environment that puts the fakes first on <c>PATH</c>; <paramref name="dotnetExit"/> is the fake dotnet's exit code.</summary>
    public IReadOnlyDictionary<string, string> Environment(int dotnetExit = 0) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = BinDirectory + System.IO.Path.PathSeparator + System.Environment.GetEnvironmentVariable("PATH"),
            ["FAKE_TOOLS_LOG"] = LogFile,
            ["FAKE_DOTNET_EXIT"] = dotnetExit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["FAKE_PNPM_EXIT"] = "0",
        };

    /// <summary>The logged invocations, in order (<c>&lt;tool&gt; &lt;argv joined by spaces&gt;</c>).</summary>
    public IReadOnlyList<string> Calls() =>
        File.Exists(LogFile)
            ? [.. File.ReadAllLines(LogFile).Where(line => line.Length > 0)]
            : [];

    /// <summary>How a command printed by the script is logged once the shell removed its quotes.</summary>
    public static string AsLogged(string command) => command.Replace("\"", string.Empty, StringComparison.Ordinal);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must not fail the run.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort.
        }
    }

    private void WriteTool(string name, string exitVariable)
    {
        var file = System.IO.Path.Combine(BinDirectory, name);
        File.WriteAllText(
            file,
            $"#!/bin/sh\nprintf '%s\\n' \"{name} $*\" >> \"$FAKE_TOOLS_LOG\"\nexit \"${{{exitVariable}:-0}}\"\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                file,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }
}
