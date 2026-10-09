using Xunit;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// The Rounds integration tests. They share one Redis (leader lock <c>rounds:leader:demo</c> and the SignalR backplane) and
/// assert wall-clock timings (IT-01, IT-04), so the collection runs alone, without parallelization.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RoundsIntegration : ICollectionFixture<RoundsContainers>
{
    public const string Name = "rounds-integration";
}
