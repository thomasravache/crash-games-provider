using Xunit;

namespace Apogeu.Ci.Tests.Live;

/// <summary>
/// The live tests share one repository and its main branch (IT-05 merges into it), so they run one at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CiLiveDefinition
{
    public const string Name = "CI live (GitHub)";
}
