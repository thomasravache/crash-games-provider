namespace Apogeu.Rounds.Infrastructure.Persistence;

/// <summary>
/// A row of <c>rounds.rounds</c>. The crash point and the revealed seed are written only once the round crashed (SPEC-0008
/// §4): before that both columns are null, so nothing persisted exposes the outcome of a round in play.
/// </summary>
internal sealed class RoundRecord
{
    public Guid Id { get; set; }

    public string SalaId { get; set; } = string.Empty;

    public long Number { get; set; }

    public Guid ChainId { get; set; }

    public int ChainRoundIndex { get; set; }

    public string Commitment { get; set; } = string.Empty;

    /// <summary>The state in lowercase (<c>betting</c>, <c>flying</c>, <c>crashed</c>, <c>annulled</c>).</summary>
    public string State { get; set; } = string.Empty;

    public DateTimeOffset BettingEndsAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CrashedAt { get; set; }

    public decimal? CrashPoint { get; set; }

    public string? RevealedSeed { get; set; }

    public string? AnnulReason { get; set; }

    public DateTimeOffset? AnnulledAt { get; set; }
}
