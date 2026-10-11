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

    /// <summary>
    /// Age after which a bet reservation whose debit was not confirmed is voided and its debit reversed (emenda 3); well above
    /// a normal wallet answer, so a debit reversed by the sweeper is reversed within 30 s even if the instance died.
    /// </summary>
    public TimeSpan PendingTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How often each instance sweeps the reservations older than <see cref="PendingTimeout"/>.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(2);
}
