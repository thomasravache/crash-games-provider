namespace Apogeu.Rounds.Domain;

/// <summary>Persistence of the chains the rooms play (schema <c>rounds</c>): the public data the verification shows.</summary>
public interface IRoundChainStore
{
    Task<RoundChain?> FindAsync(Guid chainId, CancellationToken ct);

    /// <summary>Records a chain created by the conductor (idempotent by chain id).</summary>
    Task AddAsync(RoundChain chain, CancellationToken ct);
}
