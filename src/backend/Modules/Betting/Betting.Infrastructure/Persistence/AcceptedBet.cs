using Apogeu.Betting.Domain;
using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>A persisted bet and the result it was accepted with (repeated for its <c>clientBetId</c>).</summary>
/// <param name="Bet">The bet as it is now.</param>
/// <param name="SalaId">Room of the bet.</param>
/// <param name="AcceptedStatus"><see cref="BetStatus.Placed"/> or <see cref="BetStatus.Queued"/>, when accepted.</param>
/// <param name="AcceptedRoundId">Round of the bet when accepted.</param>
/// <param name="AcceptedRoundNumber">Number of that round.</param>
/// <param name="BalanceAfter">Balance right after the debit.</param>
public sealed record AcceptedBet(
    Bet Bet,
    string SalaId,
    BetStatus AcceptedStatus,
    RoundId? AcceptedRoundId,
    long? AcceptedRoundNumber,
    decimal BalanceAfter);
