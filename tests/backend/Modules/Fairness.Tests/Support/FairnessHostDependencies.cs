using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>Real PostgreSQL and Redis (Testcontainers) for the tests on the real Host (SPEC-0065); each test owns a database.</summary>
[CollectionDefinition(Name)]
public sealed class FairnessHostDependencies : ICollectionFixture<PostgreSqlContainerFixture>, ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "fairness-host-dependencies";
}
