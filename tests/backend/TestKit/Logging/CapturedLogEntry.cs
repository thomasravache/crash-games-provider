using Microsoft.Extensions.Logging;

namespace Apogeu.TestKit.Logging;

/// <summary>A log entry captured by <see cref="LogCapture"/>: category, level, event, rendered message and exception.</summary>
public sealed record CapturedLogEntry(string Category, LogLevel Level, EventId EventId, string Message, Exception? Exception);
