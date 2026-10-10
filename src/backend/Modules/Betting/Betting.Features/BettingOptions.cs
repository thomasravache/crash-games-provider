using Apogeu.Betting.Infrastructure;

namespace Apogeu.Betting.Features;

/// <summary>Configuration section <c>Betting</c>.</summary>
public sealed class BettingOptions
{
    public const string SectionName = "Betting";

    /// <summary>Largest value accepted for <see cref="MaxPoolSize"/>: PostgreSQL's default <c>max_connections</c>.</summary>
    public const int MaxPoolSizeLimit = 100;

    /// <summary>
    /// Most PostgreSQL connections the module opens per instance (Npgsql <c>Maximum Pool Size</c> of its own pool); a burst
    /// waits for a free connection instead of opening more.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Range(1, MaxPoolSizeLimit)]
    public int MaxPoolSize { get; set; } = BettingInfrastructure.DefaultMaxPoolSize;
}
