using Xunit;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>
/// The timed unit tests (UT-07). With parallelization disabled, xUnit runs this collection alone, after the parallel ones,
/// so the heavy tests (UT-06, IT-01) do not share the CPU with the measurement (SPEC-0007 §7.3).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimedUnitTests
{
    public const string Name = "fairness-timed-unit";
}
