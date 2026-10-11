using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>One real PostgreSQL container (Testcontainers) for the ledger tests; each test owns a database.</summary>
[CollectionDefinition(Name)]
public sealed class WalletPostgres : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string Name = "wallet-postgres";
}
