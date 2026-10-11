using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features;

/// <summary>Structured logs of the Betting module: ids and codes only, never a token.</summary>
internal static partial class BettingLog
{
    [LoggerMessage(EventId = 2201, Level = LogLevel.Warning, Message = "Debit of bet {BetId} uncertain ({Result}); its reversal is scheduled.")]
    public static partial void DebitUncertain(ILogger logger, Guid betId, string result);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Information, Message = "Debit of bet {BetId} reversed ({Result}).")]
    public static partial void DebitReversed(ILogger logger, Guid betId, string result);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Warning, Message = "Refund of cancelled bet {BetId} unconfirmed ({Result}); it is retried.")]
    public static partial void RefundPending(ILogger logger, Guid betId, string result);

    [LoggerMessage(EventId = 2205, Level = LogLevel.Warning, Message = "Reconciliation: the wallet has no debit to reverse for bet {BetId}; nothing was refunded")]
    public static partial void ReconciliationNoDebit(ILogger logger, Guid betId);

    [LoggerMessage(EventId = 2206, Level = LogLevel.Warning, Message = "Pending reservation of bet {BetId} voided by the sweeper; its debit is reversed")]
    public static partial void ReservationSwept(ILogger logger, Guid betId);

    [LoggerMessage(EventId = 2207, Level = LogLevel.Error, Message = "The sweep of pending bet reservations failed; it runs again")]
    public static partial void SweepFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2208, Level = LogLevel.Warning, Message = "The confirmation of bet {BetId} had no answer; the sweeper settles its reservation")]
    public static partial void ConfirmationUnknown(ILogger logger, Guid betId, Exception exception);

    [LoggerMessage(EventId = 2209, Level = LogLevel.Warning, Message = "BetActivated of bet {BetId} could not be sent")]
    public static partial void ActivationNotSent(ILogger logger, Guid betId, Exception exception);

    [LoggerMessage(EventId = 2211, Level = LogLevel.Warning, Message = "Reconciliation: the debit of bet {BetId} was applied after its reservation was voided; a new reversal is stored")]
    public static partial void ReconciliationLateDebit(ILogger logger, Guid betId);

    [LoggerMessage(EventId = 2212, Level = LogLevel.Warning, Message = "The debit of bet {BetId} was applied after the bet was refused; its reservation is voided and the debit reversed")]
    public static partial void LateDebitVoided(ILogger logger, Guid betId);

    [LoggerMessage(EventId = 2213, Level = LogLevel.Error, Message = "The late debit of bet {BetId} could not be settled; the sweeper and the reconciliation remain")]
    public static partial void LateDebitFailed(ILogger logger, Guid betId, Exception exception);

    [LoggerMessage(EventId = 2214, Level = LogLevel.Error, Message = "The refund answer of cancelled bet {BetId} could not be recorded; it is answered as pending")]
    public static partial void RefundNotRecorded(ILogger logger, Guid betId, Exception exception);

    [LoggerMessage(EventId = 2204, Level = LogLevel.Information, Message = "{Count} queued bets of room {SalaId} entered round {RoundId}.")]
    public static partial void QueuedActivated(ILogger logger, int count, string salaId, Guid roundId);

    [LoggerMessage(EventId = 2220, Level = LogLevel.Warning, Message = "Credit of the cash-out of bet {BetId} unconfirmed ({Result}); it is retried")]
    public static partial void CreditPending(ILogger logger, Guid betId, string result);

    [LoggerMessage(EventId = 2221, Level = LogLevel.Information, Message = "Credit of the cash-out of bet {BetId} confirmed")]
    public static partial void CreditConfirmed(ILogger logger, Guid betId);

    [LoggerMessage(EventId = 2222, Level = LogLevel.Critical, Message = "Credit of the cash-out of bet {BetId} refused permanently by the wallet; reconciliation needed")]
    public static partial void CreditRefused(ILogger logger, Guid betId);

    [LoggerMessage(EventId = 2223, Level = LogLevel.Warning, Message = "The leader did not decide the cash-out of bet {BetId} in time ({Reason}); it is decided later")]
    public static partial void CashOutPending(ILogger logger, Guid betId, string reason);

    [LoggerMessage(EventId = 2224, Level = LogLevel.Warning, Message = "A cash-out message of bet {BetId} could not be sent to the player")]
    public static partial void CashOutNotSent(ILogger logger, Guid betId, Exception exception);

    [LoggerMessage(EventId = 2225, Level = LogLevel.Warning, Message = "The automatic cash-out of bet {BetId} could not be scheduled with the leader")]
    public static partial void AutoCashOutNotScheduled(ILogger logger, Guid betId, Exception exception);

    [LoggerMessage(EventId = 2226, Level = LogLevel.Warning, Message = "The automatic cash-out of bet {BetId} was executed by the leader but the bet is {Status}")]
    public static partial void AutoCashOutIgnored(ILogger logger, Guid betId, string status);
}
