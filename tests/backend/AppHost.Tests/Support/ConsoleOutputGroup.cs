using Xunit;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>Tests that redirect the process console must not run alongside other tests.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleOutputGroup
{
    public const string Name = "Console output";
}
