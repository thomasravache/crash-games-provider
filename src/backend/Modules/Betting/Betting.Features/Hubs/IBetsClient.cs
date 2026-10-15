namespace Apogeu.Betting.Features.Hubs;

/// <summary>Methods the server calls on the connections of one player of <see cref="BetsHub"/> (SPEC-0022 §6, SPEC-0023 §6, SPEC-0024 §6), and on the group of a room (SPEC-0025 §6).</summary>
public interface IBetsClient
{
    Task BetActivated(BetActivatedMessage message);

    /// <summary>An automatic cash-out executed, or the late decision of a <see cref="CashOutPending"/> that accepted it.</summary>
    Task CashedOut(CashedOut message);

    /// <summary>The late decision of a <see cref="CashOutPending"/> that refused it.</summary>
    Task CashOutRejected(CashOutRejected message);

    /// <summary>A credit retried later was confirmed.</summary>
    Task BalanceUpdated(BalanceUpdatedMessage message);

    /// <summary>The bet's round crashed while it was active without a cash-out (SPEC-0024).</summary>
    Task BetSettled(BetSettled message);

    /// <summary>The bet's round was annulled and its amount refunded (SPEC-0024).</summary>
    Task BetRefunded(BetRefunded message);

    /// <summary>The bets and cash-outs of the room in the last 200 ms window, with the round totals (SPEC-0025), to the room's group.</summary>
    Task LiveBetsBatch(LiveBetsBatch batch);
}
