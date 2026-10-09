using Microsoft.Extensions.Logging;

namespace Apogeu.Fairness.Infrastructure;

/// <summary>Log events of the Fairness module: chain ids, room ids and round indexes only, never a seed (ADR-0014).</summary>
internal static partial class FairnessLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Seed chain {ChainId} created for room {SalaId} with {Length} links")]
    public static partial void ChainCreated(ILogger logger, Guid chainId, Guid salaId, int length);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Round {RoundIndex} issued from seed chain {ChainId} of room {SalaId}; {Remaining} rounds left")]
    public static partial void RoundIssued(ILogger logger, int roundIndex, Guid chainId, Guid salaId, int remaining);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "No round left in the active seed chain of room {SalaId}")]
    public static partial void ChainExhausted(ILogger logger, Guid salaId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Round {RoundIndex} of seed chain {ChainId} revealed")]
    public static partial void RoundRevealed(ILogger logger, int roundIndex, Guid chainId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Crash point of round {RoundIndex} of seed chain {ChainId} computed")]
    public static partial void CrashPointRead(ILogger logger, int roundIndex, Guid chainId);
}
