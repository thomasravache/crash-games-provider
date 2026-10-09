using Microsoft.Extensions.Logging;

namespace Apogeu.Operators.Features.Observability;

/// <summary>
/// Log events of the Operators module (SPEC-0009 §8). Only identifiers and contract codes: never a secret, a signature, a
/// launch or session token, nor a client address (ADR-0014).
/// </summary>
internal static partial class OperatorsLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Launch issued for operator {OperatorId} (key {KeyId}) in sala {SalaId}")]
    public static partial void LaunchIssued(ILogger logger, string operatorId, string keyId, string salaId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Launch rejected with {Reason} (key {KeyId})")]
    public static partial void LaunchRejected(ILogger logger, string reason, string? keyId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Replayed operator request rejected (key {KeyId})")]
    public static partial void ReplayDetected(ILogger logger, string keyId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Rate limit reached on {Endpoint}")]
    public static partial void RateLimited(ILogger logger, string endpoint);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Player session opened for operator {OperatorId} in sala {SalaId}")]
    public static partial void SessionOpened(ILogger logger, string operatorId, string salaId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Player session refused with {Reason}")]
    public static partial void SessionRefused(ILogger logger, string reason);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Security:PlayerSession:SigningKey is not configured: player sessions are signed with an ephemeral key of this process (Development only)")]
    public static partial void EphemeralSessionKey(ILogger logger);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "API key {KeyId} belongs to operator {OperatorId}, which is not registered")]
    public static partial void OperatorNotRegistered(ILogger logger, string keyId, string operatorId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Operator {OperatorId} ensured in sala {SalaId} (Development seed)")]
    public static partial void DemoOperatorSeeded(ILogger logger, string operatorId, string salaId);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Game {GameCode} and default limits ensured for operator {OperatorId} (Development seed)")]
    public static partial void CatalogSeeded(ILogger logger, string operatorId, string gameCode);
}
