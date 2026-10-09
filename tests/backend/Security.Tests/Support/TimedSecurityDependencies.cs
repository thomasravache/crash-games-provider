using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Support;

/// <summary>
/// The timed integration test (IT-01) with containers of its own. With parallelization disabled, xUnit runs this
/// collection alone, after the parallel ones, so other tests do not share the CPU with the measurement.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimedSecurityDependencies : ICollectionFixture<PostgreSqlContainerFixture>, ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "security-timed-dependencies";
}
