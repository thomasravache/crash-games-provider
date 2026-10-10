using System.ComponentModel.DataAnnotations;
using Apogeu.Wallet.Infrastructure;

namespace Apogeu.Wallet.Features;

/// <summary>Configuration section <c>Wallet</c>: the size of the module's database connection pool (<c>Wallet:MaxPoolSize</c>).</summary>
public sealed class WalletOptions
{
    public const string SectionName = "Wallet";

    /// <summary>
    /// Most PostgreSQL connections the module opens per instance (Npgsql <c>Maximum Pool Size</c> of its own pool); a burst
    /// waits for a free connection instead of opening more (SPEC-0032). At most PostgreSQL's default <c>max_connections</c>.
    /// </summary>
    [Range(1, 100)]
    public int MaxPoolSize { get; set; } = WalletInfrastructure.DefaultMaxPoolSize;
}
