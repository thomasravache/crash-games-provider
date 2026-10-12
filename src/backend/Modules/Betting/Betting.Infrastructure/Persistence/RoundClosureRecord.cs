namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>
/// Row of <c>betting.round_closures</c>: Betting processed the end of the round (<c>crashed</c> or <c>annulled</c>, SPEC-0024),
/// so its summary may close once no bet of it is open.
/// </summary>
internal sealed class RoundClosureRecord
{
    public Guid RoundId { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public DateTimeOffset ClosedAt { get; set; }
}
