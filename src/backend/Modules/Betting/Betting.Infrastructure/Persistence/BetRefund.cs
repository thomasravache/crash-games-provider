namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>The refund a cancellation answered (contract v3): repeated by a new <c>CancelBet</c> of the same bet.</summary>
/// <param name="Refunded">Amount refunded (0.00 when the wallet had no debit to reverse).</param>
/// <param name="Confirmed">Whether the wallet confirmed it (<c>refunded</c>) or it is still pending.</param>
/// <param name="Balance">Balance after the refund, when confirmed.</param>
public sealed record BetRefund(decimal Refunded, bool Confirmed, decimal? Balance);
