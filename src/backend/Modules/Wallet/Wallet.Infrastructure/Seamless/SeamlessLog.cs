using Microsoft.Extensions.Logging;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>Structured logs of the seamless wallet (SPEC-0046): operator, operation and transaction ids only, never a secret (ADR-0014).</summary>
internal static partial class SeamlessLog
{
    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Alert: the wallet of operator {OperatorId} answered PLAYER_NOT_FOUND to {Operation} {TransactionId}; it is not resent")]
    public static partial void PlayerNotFound(ILogger logger, string operatorId, string operation, string transactionId);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Information, Message = "The wallet of operator {OperatorId} gave no definitive answer to {Operation} {TransactionId} ({Reason})")]
    public static partial void Uncertain(ILogger logger, string operatorId, string operation, string transactionId, string reason);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Error, Message = "Operator {OperatorId} has no seamless wallet configured (seamless mode with a wallet URL and a key); {Operation} {TransactionId} is not sent")]
    public static partial void NotConfigured(ILogger logger, string operatorId, string operation, string transactionId);

    [LoggerMessage(EventId = 2104, Level = LogLevel.Warning, Message = "The rollback {TransactionId} was confirmed by the operator but its debit {OriginalTransactionId} was never mirrored; the mirror ledger records neither (reconcile)")]
    public static partial void RollbackWithoutMirroredDebit(ILogger logger, string transactionId, string originalTransactionId);

    [LoggerMessage(EventId = 2105, Level = LogLevel.Warning, Message = "The mirror ledger could not record {TransactionId}; the operation is reported uncertain")]
    public static partial void MirrorFailed(ILogger logger, string transactionId, Exception exception);

    [LoggerMessage(EventId = 2106, Level = LogLevel.Information, Message = "{Operation} {TransactionId} is pending at the operator wallet of {OperatorId}; resend {Attempt} in {Delay}")]
    public static partial void ResendScheduled(ILogger logger, string operation, string transactionId, string operatorId, int attempt, TimeSpan delay);

    [LoggerMessage(EventId = 2107, Level = LogLevel.Error, Message = "{Operation} {TransactionId} to the wallet of operator {OperatorId} failed ({Reason}); it stays pending and is resent")]
    public static partial void AttemptFailed(ILogger logger, string operation, string transactionId, string operatorId, string reason, Exception exception);

    [LoggerMessage(EventId = 2108, Level = LogLevel.Error, Message = "{Operation} {TransactionId} of operator {OperatorId} could not be scheduled to resend; it is reported uncertain")]
    public static partial void PendingNotStored(ILogger logger, string operation, string transactionId, string operatorId, Exception exception);

    [LoggerMessage(EventId = 2109, Level = LogLevel.Error, Message = "The wallet URL of operator {OperatorId} is plain http to a host that is not the loopback; {Operation} {TransactionId} is refused")]
    public static partial void InsecureWalletUrl(ILogger logger, string operatorId, string operation, string transactionId);
}
