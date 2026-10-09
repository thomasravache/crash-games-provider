using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Operators.Tests.Support;

/// <summary>One real PostgreSQL container (Testcontainers) for the catalog persistence tests; each test owns a database.</summary>
[CollectionDefinition(Name)]
public sealed class OperatorsPostgres : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string Name = "operators-postgres";
}
