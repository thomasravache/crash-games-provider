using Microsoft.Extensions.Logging;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>Structured logs of the seamless wallet (SPEC-0046): operator, operation and transaction ids only, never a secret (ADR-0014).</summary>
internal static partial class SeamlessLog
{
    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Alert: the wallet of operator {OperatorId} answered PLAYER_NOT_FOUND to {Operation} {TransactionId}; it is not resent")]
    public static partial void PlayerNotFound(ILogger logger, string operatorId, string operation, string transactionId);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Information, Message = "The wallet of operator {OperatorId} gave no definitive answer to {Operation} {TransactionId} ({Reason})")]
    public static partial void Uncertain(ILogger logger, string operatorId, string operation, string transactionId, string reason);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Error, Message = "Operator {OperatorId} is in seamless mode without a wallet URL or key; {Operation} {TransactionId} is refused")]
    public static partial void NotConfigured(ILogger logger, string operatorId, string operation, string transactionId);

    [LoggerMessage(EventId = 2104, Level = LogLevel.Information, Message = "The rollback {TransactionId} has no mirrored debit {OriginalTransactionId}; the mirror ledger records neither")]
    public static partial void RollbackWithoutMirroredDebit(ILogger logger, string transactionId, string originalTransactionId);

    [LoggerMessage(EventId = 2105, Level = LogLevel.Warning, Message = "The mirror ledger could not record {TransactionId}; the operation is reported uncertain")]
    public static partial void MirrorFailed(ILogger logger, string transactionId, Exception exception);

    [LoggerMessage(EventId = 2106, Level = LogLevel.Information, Message = "{Operation} {TransactionId} is pending at the operator wallet; resend {Attempt} due at {NextAttemptAt}")]
    public static partial void ResendScheduled(ILogger logger, string operation, string transactionId, int attempt, DateTimeOffset nextAttemptAt);

    [LoggerMessage(EventId = 2107, Level = LogLevel.Warning, Message = "The sweep of pending operator wallet resends failed; it runs again")]
    public static partial void SweepFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2108, Level = LogLevel.Warning, Message = "{Operation} {TransactionId} could not be stored to resend; it is reported uncertain")]
    public static partial void PendingNotStored(ILogger logger, string operation, string transactionId, Exception exception);
}
