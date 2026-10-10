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

    [LoggerMessage(EventId = 2204, Level = LogLevel.Information, Message = "{Count} queued bets of room {SalaId} entered round {RoundId}.")]
    public static partial void QueuedActivated(ILogger logger, int count, string salaId, Guid roundId);
}
