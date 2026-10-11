using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Hubs;

/// <summary>
/// Result of <c>CancelBet</c> (SPEC-0022 §6, contract v3): the bet is cancelled and its amount refunded — confirmed by the
/// wallet (<see cref="RefundStatus.Refunded"/>, with the balance) or still being retried (<see cref="RefundStatus.Pending"/>,
/// no balance yet). A new <c>CancelBet</c> of the same bet answers the same result.
/// </summary>
/// <param name="BetId">The bet.</param>
/// <param name="Refunded">Amount refunded: the bet's amount, or 0.00 when the wallet had no debit to reverse.</param>
/// <param name="RefundStatus"><c>refunded</c> or <c>pending</c>.</param>
/// <param name="Balance">Balance after the refund; <see langword="null"/> while pending.</param>
public sealed record BetCancelled(Guid BetId, Money Refunded, RefundStatus RefundStatus, Money? Balance);
