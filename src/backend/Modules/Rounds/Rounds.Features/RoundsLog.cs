using Microsoft.Extensions.Logging;

namespace Apogeu.Rounds.Features;

/// <summary>
/// Log events of the Rounds module: room ids, round ids, numbers and states only. Neither the crash point nor the seed of a
/// round in play is ever logged (SPEC-0008 §4, ADR-0014); the crash point is logged only after the crash.
/// </summary>
internal static partial class RoundsLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Round {RoundNumber} ({RoundId}) of room {SalaId} open for bets until {BettingEndsAt}")]
    public static partial void BettingOpened(ILogger logger, string salaId, long roundNumber, Guid roundId, DateTimeOffset bettingEndsAt);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Round {RoundNumber} ({RoundId}) of room {SalaId} flying since {StartedAt}")]
    public static partial void FlightStarted(ILogger logger, string salaId, long roundNumber, Guid roundId, DateTimeOffset startedAt);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Round {RoundNumber} ({RoundId}) of room {SalaId} crashed at {CrashPoint} ({CrashedAt})")]
    public static partial void Crashed(ILogger logger, string salaId, long roundNumber, Guid roundId, decimal crashPoint, DateTimeOffset crashedAt);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Round {RoundNumber} ({RoundId}) of room {SalaId} annulled: {Reason}")]
    public static partial void Annulled(ILogger logger, string salaId, long roundNumber, Guid roundId, string reason);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Round {RoundNumber} ({RoundId}) of room {SalaId} resumed in state {State}")]
    public static partial void Resumed(ILogger logger, string salaId, long roundNumber, Guid roundId, string state);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Seed chain {ChainId} created for room {SalaId} with {Length} rounds")]
    public static partial void ChainCreated(ILogger logger, string salaId, Guid chainId, int length);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Seed chain {ChainId} of room {SalaId} is not recorded by Rounds; a new chain is created so every round stays verifiable")]
    public static partial void UnknownChain(ILogger logger, string salaId, Guid chainId);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "The revealed crash point of round {RoundNumber} ({RoundId}) of room {SalaId} differs from the one read at the start of the flight")]
    public static partial void CrashPointMismatch(ILogger logger, string salaId, long roundNumber, Guid roundId);

    [LoggerMessage(EventId = 20, Level = LogLevel.Information, Message = "This instance now leads room {SalaId}")]
    public static partial void LeadershipAcquired(ILogger logger, string salaId);

    [LoggerMessage(EventId = 21, Level = LogLevel.Warning, Message = "This instance no longer leads room {SalaId}")]
    public static partial void LeadershipLost(ILogger logger, string salaId);

    [LoggerMessage(EventId = 22, Level = LogLevel.Warning, Message = "Leadership of room {SalaId} could not be checked in Redis")]
    public static partial void LeadershipUnavailable(ILogger logger, string salaId, Exception exception);

    [LoggerMessage(EventId = 23, Level = LogLevel.Error, Message = "The conductor of room {SalaId} failed; it resumes from the store")]
    public static partial void ConductorFailed(ILogger logger, string salaId, Exception exception);
}
