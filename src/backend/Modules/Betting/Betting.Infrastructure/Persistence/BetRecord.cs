namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>Row of <c>betting.bets</c>: a bet accepted (debited) and the result it was accepted with.</summary>
internal sealed class BetRecord
{
    public Guid Id { get; set; }

    public string OperatorId { get; set; } = string.Empty;

    public string PlayerId { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public string SalaId { get; set; } = string.Empty;

    public Guid ClientBetId { get; set; }

    public short Panel { get; set; }

    public decimal Amount { get; set; }

    public decimal? AutoCashOut { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid? RoundId { get; set; }

    public long? RoundNumber { get; set; }

    /// <summary>Status the bet was accepted with (<c>placed</c> or <c>queued</c>): the answer repeated for its <c>clientBetId</c>.</summary>
    public string AcceptedStatus { get; set; } = string.Empty;

    public Guid? AcceptedRoundId { get; set; }

    public long? AcceptedRoundNumber { get; set; }

    /// <summary>Balance right after the debit, as the wallet answered it (0 while the reservation is pending).</summary>
    public decimal BalanceAfter { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Amount the cancellation refunded (contract v3); <see langword="null"/> until cancelled.</summary>
    public decimal? Refunded { get; set; }

    /// <summary><c>refunded</c> or <c>pending</c>, as the cancellation answered.</summary>
    public string? RefundStatus { get; set; }

    /// <summary>Balance after the refund, when the cancellation confirmed it.</summary>
    public decimal? RefundBalance { get; set; }
}
