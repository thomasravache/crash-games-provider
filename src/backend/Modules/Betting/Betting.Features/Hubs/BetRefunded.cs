using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>
/// <c>BetRefunded</c> (SPEC-0024 §6): the bet's round was annulled and its full amount refunded — confirmed by the wallet
/// (<see cref="RefundStatus.Refunded"/>, with the balance) or still being retried (<see cref="RefundStatus.Pending"/>, no balance
/// yet), as <c>BetCancelled</c> (SPEC-0022).
/// </summary>
/// <param name="BetId">The bet.</param>
/// <param name="RoundId">Its round (the bare <see cref="Guid"/> on the wire).</param>
/// <param name="Refunded">Amount refunded: the bet's amount.</param>
/// <param name="Balance">Balance after the refund; <see langword="null"/> while pending.</param>
/// <param name="RefundStatus"><c>refunded</c> or <c>pending</c>.</param>
public sealed record BetRefunded(Guid BetId, Guid RoundId, Money Refunded, Money? Balance, RefundStatus RefundStatus);
