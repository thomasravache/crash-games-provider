using Apogeu.Rounds.Contracts;

namespace Apogeu.Betting.Contracts;

/// <summary>The settlement summaries of a round (SPEC-0024 §6): one per operator and currency, immutable once written.</summary>
public interface IRoundSettlementQuery
{
    /// <summary>The summaries of <paramref name="roundId"/>, one per (operator, currency); empty until the round is settled.</summary>
    Task<IReadOnlyList<RoundSettlement>> GetAsync(RoundId roundId, CancellationToken ct);
}
