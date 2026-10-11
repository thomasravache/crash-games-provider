namespace Apogeu.Betting.Features.Hubs;

/// <summary><c>CashOutRejected</c> (SPEC-0023 §6): the late decision of a <see cref="CashOutPending"/> refused it (<c>CASHOUT_TOO_LATE</c>).</summary>
public sealed record CashOutRejected(Guid BetId, string Code);
