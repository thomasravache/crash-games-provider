using Xunit;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>
/// Tests that start the real AppHost run one at a time and never alongside other tests: every AppHost instance mounts
/// the same named postgres data volume, and the ≤ 90 s budget of IT-01 must not be measured under parallel load.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppHostEnvironmentGroup
{
    public const string Name = "AppHost environment";
}
