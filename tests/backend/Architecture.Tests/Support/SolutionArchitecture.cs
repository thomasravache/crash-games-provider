using System.Reflection;
using System.Xml.Linq;
using ArchUnitNET.Loader;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace Apogeu.Architecture.Tests.Support;

/// <summary>
/// Architecture made of the real back-end assemblies: one per project under src/backend listed in Apogeu.slnx
/// (assembly name Apogeu.&lt;project&gt;, from Directory.Build.props). The test project references the Host, which
/// brings every module and BuildingBlocks to the output folder.
/// </summary>
internal static class SolutionArchitecture
{
    private const string BackendFolder = "src/backend/";

    private static readonly Lazy<ArchitectureModel> Loaded = new(
        () => new ArchLoader().LoadAssemblies([.. ExpectedAssemblyNames().Select(LoadFromOutput)]).Build());

    public static ArchitectureModel Instance => Loaded.Value;

    /// <summary>Assembly names expected from Apogeu.slnx (every back-end project of the solution).</summary>
    public static IReadOnlyList<string> ExpectedAssemblyNames()
    {
        var solution = XDocument.Load(RepositoryRoot.Combine("Apogeu.slnx"));
        return solution
            .Descendants("Project")
            .Select(project => ((string?)project.Attribute("Path") ?? string.Empty).Replace('\\', '/'))
            .Where(path => path.StartsWith(BackendFolder, StringComparison.Ordinal))
            .Select(path => "Apogeu." + Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Path where an expected assembly must be found in the test output.</summary>
    public static string OutputPath(string assemblyName) =>
        Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");

    private static Assembly LoadFromOutput(string assemblyName) => Assembly.LoadFrom(OutputPath(assemblyName));
}
