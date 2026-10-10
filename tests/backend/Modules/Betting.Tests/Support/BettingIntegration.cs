using Xunit;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// The Betting integration tests. They share one Redis (leader lock of the room and the SignalR backplane, flushed by each
/// test) and IT-06 asserts wall-clock latency, so the collection runs alone, without parallelization.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BettingIntegration : ICollectionFixture<BettingContainers>
{
    public const string Name = "betting-integration";
}
