using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>Real PostgreSQL for the timed tests (IT-05): a container of its own and no parallelism, so the measurement is not disturbed.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimedWalletPostgres : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string Name = "wallet-postgres-timed";
}
