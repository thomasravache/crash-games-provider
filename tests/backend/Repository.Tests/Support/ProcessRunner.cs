using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Apogeu.Repository.Tests.Support;

/// <summary>Runs external tools (dotnet, pnpm, git) capturing exit code and output, with a timeout.</summary>
public static class ProcessRunner
{
    public static readonly TimeSpan ShortTimeout = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(15);

    // Variables inherited from the outer `dotnet test` / git hook that would leak into the child tool.
    private static readonly string[] ScrubbedVariables =
    [
        "MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildSDKsPath", "DOTNET_HOST_PATH",
        "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_PREFIX",
    ];

    private static readonly Dictionary<string, string> BaseEnvironment = new(StringComparer.Ordinal)
    {
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        ["DOTNET_NOLOGO"] = "1",
        ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["UseSharedCompilation"] = "false",
        ["NO_COLOR"] = "1",
        ["FORCE_COLOR"] = "0",
    };

    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var args = arguments.ToArray();
        var commandLine = string.Join(' ', [fileName, .. args]);
        var output = new CapturedOutput();

        using var process = new Process { StartInfo = CreateStartInfo(fileName, args, workingDirectory, environment) };
        process.OutputDataReceived += (_, e) => output.Append(e.Data, isError: false);
        process.ErrorDataReceived += (_, e) => output.Append(e.Data, isError: true);

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            var message = $"Could not start '{fileName}': {exception.Message}";
            return new ProcessResult(commandLine, -1, string.Empty, exception.Message, message, TimedOut: false);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.StandardInput.Close();

        var timedOut = await WaitAsync(process, timeout, cancellationToken).ConfigureAwait(false);

        // Parameterless WaitForExit flushes the asynchronous output readers.
        process.WaitForExit();

        return output.ToResult(commandLine, timedOut ? -1 : process.ExitCode, timedOut);
    }

    public static Task<ProcessResult> DotnetAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default) =>
        RunAsync("dotnet", arguments, workingDirectory, LongTimeout, cancellationToken: cancellationToken);

    public static Task<ProcessResult> PnpmAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default) =>
        RunAsync("pnpm", arguments, workingDirectory, LongTimeout, cancellationToken: cancellationToken);

    /// <summary>Runs git isolated from the developer's global/system configuration.</summary>
    public static Task<ProcessResult> GitAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var isolatedConfig = Path.Combine(Path.GetTempPath(), "apogeu-repository-tests-gitconfig");
        Directory.CreateDirectory(isolatedConfig);
        var emptyGlobal = Path.Combine(isolatedConfig, "gitconfig");
        if (!File.Exists(emptyGlobal))
        {
            File.WriteAllText(emptyGlobal, string.Empty);
        }

        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_CONFIG_GLOBAL"] = emptyGlobal,
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["XDG_CONFIG_HOME"] = isolatedConfig,
            ["GIT_TERMINAL_PROMPT"] = "0",
        };
        return RunAsync("git", arguments, workingDirectory, ShortTimeout, environment, cancellationToken);
    }

    private static ProcessStartInfo CreateStartInfo(
        string fileName,
        string[] args,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var name in ScrubbedVariables)
        {
            startInfo.Environment.Remove(name);
        }

        foreach (var (key, value) in BaseEnvironment)
        {
            startInfo.Environment[key] = value;
        }

        foreach (var (key, value) in environment ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            startInfo.Environment[key] = value;
        }

        return startInfo;
    }

    /// <summary>Waits for the process; kills its tree and returns true on timeout.</summary>
    private static async Task<bool> WaitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancellationToken);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
    }

    private sealed class CapturedOutput
    {
        private readonly Lock _gate = new();
        private readonly StringBuilder _standardOutput = new();
        private readonly StringBuilder _standardError = new();
        private readonly StringBuilder _combined = new();

        public void Append(string? line, bool isError)
        {
            if (line is null)
            {
                return;
            }

            lock (_gate)
            {
                (isError ? _standardError : _standardOutput).AppendLine(line);
                _combined.AppendLine(line);
            }
        }

        public ProcessResult ToResult(string commandLine, int exitCode, bool timedOut)
        {
            lock (_gate)
            {
                return new ProcessResult(
                    commandLine,
                    exitCode,
                    _standardOutput.ToString(),
                    _standardError.ToString(),
                    _combined.ToString(),
                    timedOut);
            }
        }
    }
}
