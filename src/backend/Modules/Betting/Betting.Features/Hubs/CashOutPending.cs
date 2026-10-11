namespace Apogeu.Betting.Features.Hubs;

/// <summary>
/// The leader did not decide within 200 ms (SPEC-0023 §4): the request is recorded with its reception instant and decided
/// later, by <c>CashedOut</c> or <c>CashOutRejected</c>.
/// </summary>
public sealed record CashOutPending(Guid BetId);
