using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Infrastructure;

namespace Apogeu.Rounds.Features;

/// <summary>
/// Configuration section <c>Rounds</c>: the rooms by id (<c>Rounds:Salas:{id}</c>, SPEC-0008 §4) and the size of the module's
/// database connection pool (<c>Rounds:MaxPoolSize</c>) and the deadline of a shared snapshot read
/// (<c>Rounds:SnapshotReadTimeout</c>).
/// </summary>
public sealed class RoundsOptions
{
    public const string SectionName = "Rounds";

    /// <summary>Largest value accepted for <see cref="MaxPoolSize"/>: PostgreSQL's default <c>max_connections</c>.</summary>
    public const int MaxPoolSizeLimit = 100;

    public IDictionary<string, SalaOptions> Salas { get; } = new Dictionary<string, SalaOptions>(StringComparer.Ordinal);

    /// <summary>
    /// Most PostgreSQL connections the module opens per instance (Npgsql <c>Maximum Pool Size</c> of its own pool); a burst
    /// waits for a free connection instead of opening more (IMP-02).
    /// </summary>
    public int MaxPoolSize { get; set; } = RoundsInfrastructure.DefaultMaxPoolSize;

    /// <summary>
    /// Deadline of the store read that the <c>JoinSala</c> of a room not led by this instance share: past it, the waiting
    /// joins fail at once and the next batch reads again, instead of all of them waiting for Npgsql's own timeouts.
    /// </summary>
    public TimeSpan SnapshotReadTimeout { get; set; } = SalaSnapshotQuery.DefaultReadTimeout;
}
