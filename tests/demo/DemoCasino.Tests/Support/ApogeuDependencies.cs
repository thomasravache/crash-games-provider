using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>Real PostgreSQL and Redis (Testcontainers) for the real Apogeu of the lobby tests (SPEC-0044); each host owns a database.</summary>
[CollectionDefinition(Name)]
public sealed class ApogeuDependencies : ICollectionFixture<PostgreSqlContainerFixture>, ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "demo-casino-apogeu-dependencies";
}
