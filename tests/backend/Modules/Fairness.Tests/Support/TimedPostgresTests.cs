using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>
/// The timed integration tests (IT-02), with a PostgreSQL container of their own. Run alone, like <see cref="TimedUnitTests"/>.
/// It is a separate collection so that a run of the unit tests only never starts a container.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimedPostgresTests : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string Name = "fairness-timed-postgres";
}
