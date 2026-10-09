using Apogeu.Operators.Infrastructure.Persistence;

namespace Apogeu.Operators.Infrastructure;

/// <summary>Launch grants, stored by the hash of their token (SPEC-0009 §4).</summary>
public interface ILaunchGrantStore
{
    Task AddAsync(LaunchGrantRecord record, CancellationToken cancellationToken);

    /// <summary>The grant whose token hash is <paramref name="tokenHash"/>, or <see langword="null"/>.</summary>
    Task<LaunchGrantRecord?> FindAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the grant as redeemed at <paramref name="redeemedAt"/> only if nobody redeemed it before, atomically in the
    /// database: of concurrent calls for the same grant, exactly one returns <see langword="true"/>.
    /// </summary>
    Task<bool> TryMarkRedeemedAsync(string tokenHash, DateTimeOffset redeemedAt, CancellationToken cancellationToken);
}
