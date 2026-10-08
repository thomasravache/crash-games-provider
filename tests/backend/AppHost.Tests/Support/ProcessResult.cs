using System.Globalization;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>Outcome of an external process: exit code and combined output.</summary>
internal sealed record ProcessResult(string CommandLine, int ExitCode, string Output, bool TimedOut)
{
    public string Describe()
    {
        var status = TimedOut ? "TIMED OUT" : string.Create(CultureInfo.InvariantCulture, $"exit code {ExitCode}");
        var tail = Output.Length <= 6000 ? Output : "[...]" + Output[^6000..];
        return $"`{CommandLine}` -> {status}{Environment.NewLine}{tail}";
    }
}
