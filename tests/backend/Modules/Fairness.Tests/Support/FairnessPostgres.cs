using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>One real PostgreSQL container (Testcontainers) shared by the Fairness integration tests; each test owns a database.</summary>
[CollectionDefinition(Name)]
public sealed class FairnessPostgres : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string Name = "fairness-postgres";
}
