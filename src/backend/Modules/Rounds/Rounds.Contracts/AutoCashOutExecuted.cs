using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Rounds.Contracts;

/// <summary>
/// Integration event (SPEC-0023 §6, ADR-0002): the leader of <paramref name="SalaId"/> executed the automatic cash-out of
/// <paramref name="BetId"/> at its target; Betting persists the cash-out, credits it and sends <c>CashedOut{auto:true}</c>.
/// </summary>
/// <param name="SalaId">Room of the round.</param>
/// <param name="RoundId">The round.</param>
/// <param name="BetId">The bet cashed out.</param>
/// <param name="Multiplier">The target multiplier, exact.</param>
/// <param name="ExecutedAt">Server instant of <c>t_alvo</c>.</param>
public sealed record AutoCashOutExecuted(string SalaId, RoundId RoundId, Guid BetId, Multiplier Multiplier, DateTimeOffset ExecutedAt);
