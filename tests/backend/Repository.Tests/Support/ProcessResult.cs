using System.Globalization;

namespace Apogeu.Repository.Tests.Support;

/// <summary>Outcome of an external process: exit code and captured output.</summary>
public sealed record ProcessResult(
    string CommandLine,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string Output,
    bool TimedOut)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;

    /// <summary>Message for assertion failures: the command, its exit code and its output.</summary>
    public string Describe()
    {
        var status = TimedOut
            ? "TIMED OUT"
            : string.Create(CultureInfo.InvariantCulture, $"exit code {ExitCode}");
        return $"`{CommandLine}` -> {status}{Environment.NewLine}{Tail(Output, 6000)}";
    }

    private static string Tail(string text, int maxLength) =>
        text.Length <= maxLength ? text : "[...]" + text[^maxLength..];
}
