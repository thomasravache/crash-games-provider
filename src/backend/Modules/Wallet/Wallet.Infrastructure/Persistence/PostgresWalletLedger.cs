using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;

namespace Apogeu.Wallet.Infrastructure.Persistence;

#pragma warning disable MA0025 // Scaffold of SPEC-0021 (no logic yet): implemented in the green phase.

/// <summary>The ledger in the schema <c>wallet</c> of PostgreSQL (ADR-0010), insert-only.</summary>
internal sealed class PostgresWalletLedger : IWalletLedger
{
    public PostgresWalletLedger(Func<string> connectionString) => ArgumentNullException.ThrowIfNull(connectionString);

    public Task<LedgerEntry?> FindAsync(TransactionId transactionId, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<Money> SumAsync(LedgerAccount account, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<bool> TryAppendAsync(LedgerEntry entry, CancellationToken cancellationToken) => throw new NotImplementedException();
}
#pragma warning restore MA0025
