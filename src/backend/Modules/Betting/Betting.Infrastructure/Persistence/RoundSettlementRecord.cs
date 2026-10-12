namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>Row of <c>betting.round_settlements</c>: the immutable summary of a round for one operator and currency (SPEC-0024).</summary>
internal sealed class RoundSettlementRecord
{
    public Guid RoundId { get; set; }

    public string OperatorId { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public int BetsCount { get; set; }

    public decimal TotalWagered { get; set; }

    public decimal TotalPaid { get; set; }

    public decimal TotalRefunded { get; set; }

    public decimal Ggr { get; set; }

    public DateTimeOffset SettledAt { get; set; }
}
