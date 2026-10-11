using Apogeu.Betting.Domain;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>A persisted bet and the result it was accepted with (repeated for its <c>clientBetId</c>).</summary>
/// <param name="Bet">The bet as it is now (for a reservation, as it would be accepted).</param>
/// <param name="SalaId">Room of the bet.</param>
/// <param name="AcceptedStatus"><see cref="BetStatus.Placed"/> or <see cref="BetStatus.Queued"/>, when accepted.</param>
/// <param name="AcceptedRoundId">Round of the bet when accepted.</param>
/// <param name="AcceptedRoundNumber">Number of that round.</param>
/// <param name="BalanceAfter">Balance right after the debit.</param>
/// <param name="IsPending">A reservation whose debit is not confirmed yet: the bet is not accepted.</param>
/// <param name="Refund">The refund its cancellation answered, once cancelled.</param>
/// <param name="CashOut">Its cash-out, once cashed out (SPEC-0023).</param>
/// <param name="CashOutRequestedAt">Reception instant of a manual cash-out waiting for the leader's decision.</param>
public sealed record AcceptedBet(
    Bet Bet,
    string SalaId,
    BetStatus AcceptedStatus,
    RoundId? AcceptedRoundId,
    long? AcceptedRoundNumber,
    decimal BalanceAfter,
    bool IsPending = false,
    BetRefund? Refund = null,
    CashOutRecord? CashOut = null,
    DateTimeOffset? CashOutRequestedAt = null);
