namespace Apogeu.Betting.Features.Hubs;

/// <summary>
/// Result of <c>CashOut</c> (SPEC-0023 §6, contract v2): the union <c>CashedOut | CashOutPending</c>, exactly one of its
/// members set. It is a wrapper rather than a polymorphic type because the hub contract generator (SPEC-0006) expresses
/// object members, not a bare <c>anyOf</c> of objects.
/// </summary>
/// <param name="CashedOut">The bet was cashed out (<c>auto:false</c>); <see langword="null"/> when pending.</param>
/// <param name="Pending">The leader did not decide within 200 ms; <see langword="null"/> when cashed out.</param>
public sealed record CashOutResult(CashedOut? CashedOut, CashOutPending? Pending);
