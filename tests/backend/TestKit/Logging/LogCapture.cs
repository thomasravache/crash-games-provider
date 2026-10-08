using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Apogeu.TestKit.Logging;

/// <summary>
/// Captures every log entry written through the loggers it creates, so tests can assert on logs (for example that an
/// unrevealed seed never appears in them, ADR-0006). Register it with <c>ILoggingBuilder.AddProvider</c>.
/// </summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    /// <summary>The entries captured so far, in the order they were written.</summary>
    public IReadOnlyList<CapturedLogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
        // Nothing to release: the entries stay readable after the host that used the provider is disposed.
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            entries.Enqueue(new CapturedLogEntry(category, logLevel, eventId, formatter(state, exception), exception));
        }
    }
}
