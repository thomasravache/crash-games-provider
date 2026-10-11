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

    /// <summary>Multiplier of the cash-out (SPEC-0023); <see langword="null"/> until cashed out.</summary>
    public decimal? CashOutMultiplier { get; set; }

    /// <summary>Payout of the cash-out: <c>min(floor(amount × multiplier × 100)/100, maxWin)</c>.</summary>
    public decimal? Payout { get; set; }

    /// <summary>Whether the cash-out was the automatic one executed by the leader.</summary>
    public bool? CashOutAuto { get; set; }

    /// <summary>When the bet was cashed out (server clock).</summary>
    public DateTimeOffset? CashedOutAt { get; set; }

    /// <summary><c>credited</c> or <c>pending</c>: the credit of the payout (<c>bet:{betId}:credit</c>).</summary>
    public string? CreditStatus { get; set; }

    /// <summary>Balance after the credit, once the wallet confirmed it.</summary>
    public decimal? CreditBalance { get; set; }

    /// <summary>
    /// Reception instant of a manual cash-out the leader did not decide within its deadline (<c>CashOutPending</c>): it is
    /// decided later with this instant; the bet stays active meanwhile.
    /// </summary>
    public DateTimeOffset? CashOutRequestedAt { get; set; }
}
