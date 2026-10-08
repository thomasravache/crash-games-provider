using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>Runs an external tool (docker, git) capturing exit code and output, with a timeout.</summary>
internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var args = arguments.ToArray();
        var commandLine = string.Join(' ', [fileName, .. args]);
        var output = new CapturedOutput();

        using var process = new Process { StartInfo = CreateStartInfo(fileName, args, workingDirectory) };
        process.OutputDataReceived += (_, e) => output.Append(e.Data);
        process.ErrorDataReceived += (_, e) => output.Append(e.Data);

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            return new ProcessResult(commandLine, -1, $"Could not start '{fileName}': {exception.Message}", TimedOut: false);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timedOut = await WaitAsync(process, timeout, cancellationToken).ConfigureAwait(false);

        // Parameterless WaitForExit flushes the asynchronous output readers.
        process.WaitForExit();
        return new ProcessResult(commandLine, timedOut ? -1 : process.ExitCode, output.Text, timedOut);
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, string[] args, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
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
        private readonly StringBuilder _text = new();

        public string Text
        {
            get
            {
                lock (_gate)
                {
                    return _text.ToString();
                }
            }
        }

        public void Append(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (_gate)
            {
                _text.AppendLine(line);
            }
        }
    }
}
