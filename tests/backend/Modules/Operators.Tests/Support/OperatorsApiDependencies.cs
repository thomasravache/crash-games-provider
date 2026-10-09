using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Operators.Tests.Support;

/// <summary>Real PostgreSQL and Redis (Testcontainers) shared by the Host-level tests of SPEC-0020; each host owns a database.</summary>
[CollectionDefinition(Name)]
public sealed class OperatorsApiDependencies : ICollectionFixture<PostgreSqlContainerFixture>, ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "operators-api-dependencies";
}
