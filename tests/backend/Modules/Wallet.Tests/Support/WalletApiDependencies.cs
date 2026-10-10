using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>Real PostgreSQL and Redis (Testcontainers) shared by the Host-level tests of SPEC-0021; each host owns a database.</summary>
[CollectionDefinition(Name)]
public sealed class WalletApiDependencies : ICollectionFixture<PostgreSqlContainerFixture>, ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "wallet-api-dependencies";
}
