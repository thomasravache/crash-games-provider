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

    [LoggerMessage(EventId = 2204, Level = LogLevel.Information, Message = "{Count} queued bets of room {SalaId} entered round {RoundId}.")]
    public static partial void QueuedActivated(ILogger logger, int count, string salaId, Guid roundId);
}
