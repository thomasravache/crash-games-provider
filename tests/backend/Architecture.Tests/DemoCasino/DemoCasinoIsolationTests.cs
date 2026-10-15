using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.DemoCasino;

// SPEC-0043 UT-03 (ADR-0027, G3): the demo casino is an external operator; it talks to the Apogeu only over HTTP, so none
// of its assemblies references an Apogeu project. Checked at three levels: the project file (ProjectReference and
// PackageReference), the restore lock file (transitive projects and packages) and the compiled assemblies (the metadata
// references of the DemoCasino assembly and the libraries of its deps.json).
// The DemoCasino is not a reference of this test project (it is not part of the Apogeu); the test builds it with the
// configuration of this test run and inspects its output.
public sealed class DemoCasinoIsolationTests
{
    private const string ProjectFolder = "src/demo/DemoCasino";
    private const string ProjectFile = "DemoCasino.csproj";
    private const string DemoFolder = "src/demo/";

#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    [Fact(DisplayName = "Dados os assemblies do DemoCasino, quando inspecionados, então não referenciam projetos Apogeu.*")]
    [Trait("Spec", "SPEC-0043:UT-03")]
    [Trait("Category", "Architecture")]
    public async Task GivenDemoCasinoAssembliesWhenInspectedThenTheyReferenceNoApogeuProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectPath = RepositoryRoot.Combine(ProjectFolder, ProjectFile);
        Assert.True(File.Exists(projectPath), $"{ProjectFolder}/{ProjectFile} not found (ADR-0027: the demo casino lives in {ProjectFolder}).");

        var violations = new List<string>();
        violations.AddRange(ProjectFileViolations(projectPath));
        violations.AddRange(LockFileViolations(Path.Combine(Path.GetDirectoryName(projectPath)!, "packages.lock.json")));

        var targetPath = await BuildAsync(projectPath, ct);
        var ownName = Path.GetFileNameWithoutExtension(targetPath);
        violations.AddRange(AssemblyReferenceViolations(targetPath, ownName));
        violations.AddRange(DepsFileViolations(Path.ChangeExtension(targetPath, ".deps.json"), ownName));

        Assert.True(
            violations.Count == 0,
            "The DemoCasino references Apogeu projects (ADR-0027: HTTP only):" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>Forbidden: an Apogeu name, i.e. Apogeu.* except the DemoCasino's own assemblies (Directory.Build.props names them Apogeu.&lt;project&gt;).</summary>
    private static bool IsApogeuName(string name, string ownName) =>
        name.StartsWith("Apogeu.", StringComparison.OrdinalIgnoreCase)
        && !name.StartsWith(ownName, StringComparison.OrdinalIgnoreCase)
        && !name.StartsWith("Apogeu.DemoCasino", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> ProjectFileViolations(string projectPath)
    {
        var project = XDocument.Load(projectPath);
        var folder = Path.GetDirectoryName(projectPath)!;
        foreach (var reference in project.Descendants().Where(e => string.Equals(e.Name.LocalName, "ProjectReference", StringComparison.Ordinal)))
        {
            var include = ((string?)reference.Attribute("Include") ?? string.Empty).Replace('\\', '/');
            var full = Path.GetFullPath(Path.Combine(folder, include));
            var relative = Path.GetRelativePath(RepositoryRoot.Path, full).Replace('\\', '/');
            if (!relative.StartsWith(DemoFolder, StringComparison.Ordinal))
            {
                yield return $"{ProjectFile}: ProjectReference to {relative} (outside {DemoFolder})";
            }
        }

        foreach (var package in project.Descendants().Where(e => string.Equals(e.Name.LocalName, "PackageReference", StringComparison.Ordinal)))
        {
            var id = (string?)package.Attribute("Include") ?? string.Empty;
            if (IsApogeuName(id, "Apogeu.DemoCasino"))
            {
                yield return $"{ProjectFile}: PackageReference {id}";
            }
        }
    }

    private static List<string> LockFileViolations(string lockPath)
    {
        if (!File.Exists(lockPath))
        {
            return [$"{ProjectFolder}/packages.lock.json missing (ADR-0016: every project restores with a lock file)"];
        }

        using var lockFile = JsonDocument.Parse(File.ReadAllText(lockPath));
        if (!lockFile.RootElement.TryGetProperty("dependencies", out var frameworks))
        {
            return [];
        }

        return
        [
            .. frameworks.EnumerateObject()
                .SelectMany(framework => framework.Value.EnumerateObject())
                .Where(dependency => IsApogeuName(dependency.Name, "Apogeu.DemoCasino")
                    || (dependency.Value.TryGetProperty("type", out var type)
                        && string.Equals(type.GetString(), "Project", StringComparison.OrdinalIgnoreCase)
                        && !dependency.Name.Contains("democasino", StringComparison.OrdinalIgnoreCase)))
                .Select(dependency => $"packages.lock.json: {dependency.Name}")
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static List<string> AssemblyReferenceViolations(string assemblyPath, string ownName)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        return
        [
            .. metadata.AssemblyReferences
                .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
                .Where(name => IsApogeuName(name, ownName))
                .Select(name => $"{ownName}.dll references the assembly {name}"),
        ];
    }

    private static List<string> DepsFileViolations(string depsPath, string ownName)
    {
        if (!File.Exists(depsPath))
        {
            return [$"{Path.GetFileName(depsPath)} missing next to the DemoCasino assembly"];
        }

        using var deps = JsonDocument.Parse(File.ReadAllText(depsPath));
        if (!deps.RootElement.TryGetProperty("libraries", out var libraries))
        {
            return [];
        }

        return
        [
            .. libraries.EnumerateObject()
                .Select(library => library.Name.Split('/')[0])
                .Where(name => IsApogeuName(name, ownName))
                .Select(name => $"{Path.GetFileName(depsPath)}: library {name}"),
        ];
    }

    /// <summary>Builds the DemoCasino (a no-op when up to date) and returns the path of its main assembly.</summary>
    private static async Task<string> BuildAsync(string projectPath, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepositoryRoot.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "build", projectPath, "-c", Configuration, "--nologo", "-v", "q", "-getProperty:TargetPath" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Variables of the outer `dotnet test` that would leak into the child build.
        foreach (var name in new[] { "MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildSDKsPath", "DOTNET_HOST_PATH" })
        {
            startInfo.Environment.Remove(name);
        }

        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        var stdout = (await output.ConfigureAwait(false)).Trim();
        Assert.True(process.ExitCode == 0, $"`dotnet build {ProjectFolder}` failed:{Environment.NewLine}{stdout}{Environment.NewLine}{await error.ConfigureAwait(false)}");

        var targetPath = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last();
        Assert.True(File.Exists(targetPath), $"DemoCasino assembly not found at '{targetPath}'.");
        return targetPath;
    }
}
