using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Support;

/// <summary>Real PostgreSQL and Redis (Testcontainers) shared by the Host-level security tests; each host owns a database.</summary>
[CollectionDefinition(Name)]
public sealed class SecurityDependencies : ICollectionFixture<PostgreSqlContainerFixture>, ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "security-dependencies";
}
