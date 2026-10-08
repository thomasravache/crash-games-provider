using Apogeu.Architecture.Fixtures;
using ArchUnitNET.Loader;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace Apogeu.Architecture.Tests.Support;

/// <summary>
/// Architecture made only of the fixture assemblies with known violations (Fixtures/**): the fixture module types and
/// the MediatR stand-in. Never mixed with the real assemblies.
/// </summary>
internal static class FixtureArchitecture
{
    private static readonly Lazy<ArchitectureModel> Loaded = new(
        () => new ArchLoader()
            .LoadAssemblies(typeof(FixtureAssemblyMarker).Assembly, typeof(MediatR.IMediator).Assembly)
            .Build());

    public static ArchitectureModel Instance => Loaded.Value;
}
