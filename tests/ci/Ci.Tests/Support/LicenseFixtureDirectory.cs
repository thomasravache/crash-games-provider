using System.Text.Json;
using Apogeu.Repository.Tests.Support;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Temporary copy of the repository layout that tools/ci/check-licenses expects, deleted on dispose: the script itself
/// under tools/ci, an empty obj/project.assets.json (no NuGet package) and a fake <c>pnpm</c> first on PATH that prints
/// the given npm packages as <c>pnpm licenses list --json</c> does (only the production ones with <c>--prod</c>).
/// </summary>
public sealed class LicenseFixtureDirectory : IDisposable
{
    public const string Script = "tools/ci/check-licenses";

    private LicenseFixtureDirectory(string path) => Path = path;

    public string Path { get; }

    public static LicenseFixtureDirectory Create(IReadOnlyCollection<NpmPackage> packages)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "apogeu-ci-licenses-" + Guid.NewGuid().ToString("N"));
        var fixture = new LicenseFixtureDirectory(path);

        var tools = System.IO.Path.Combine(path, "tools", "ci");
        Directory.CreateDirectory(tools);
        File.Copy(System.IO.Path.Combine(RepositoryRoot.Path, Script), System.IO.Path.Combine(tools, "check-licenses"));

        var obj = System.IO.Path.Combine(path, "obj");
        Directory.CreateDirectory(obj);
        File.WriteAllText(
            System.IO.Path.Combine(obj, "project.assets.json"),
            """{ "version": 3, "libraries": {}, "packageFolders": {} }""");

        Directory.CreateDirectory(System.IO.Path.Combine(path, "src", "frontend"));

        var bin = System.IO.Path.Combine(path, "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(System.IO.Path.Combine(bin, "all.json"), LicencesJson(packages));
        File.WriteAllText(System.IO.Path.Combine(bin, "prod.json"), LicencesJson([.. packages.Where(package => package.Production)]));
        var pnpm = System.IO.Path.Combine(bin, "pnpm");
        File.WriteAllText(
            pnpm,
            """
            #!/bin/sh
            here=$(cd "$(dirname "$0")" && pwd)
            for argument in "$@"; do
              if [ "$argument" = "--prod" ]; then cat "$here/prod.json"; exit 0; fi
            done
            cat "$here/all.json"
            """.ReplaceLineEndings("\n"));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(pnpm, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return fixture;
    }

    /// <summary>Runs the copied script from the fixture root with the fake pnpm first on PATH.</summary>
    public Task<ProcessResult> RunCheckLicensesAsync(CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = System.IO.Path.Combine(Path, "bin") + System.IO.Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
        };
        return ProcessRunner.RunAsync(
            "python3",
            [System.IO.Path.Combine(Path, "tools", "ci", "check-licenses")],
            Path,
            ProcessRunner.ShortTimeout,
            environment,
            cancellationToken);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must not fail the run.
        }
    }

    private static string LicencesJson(IReadOnlyCollection<NpmPackage> packages) =>
        JsonSerializer.Serialize(packages
            .GroupBy(package => package.Licence, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(package => new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["name"] = package.Name,
                    ["versions"] = new[] { package.Version },
                    ["license"] = package.Licence,
                }).ToList(),
                StringComparer.Ordinal));

    /// <summary>One npm package as listed by pnpm; <paramref name="Production"/> = also listed with <c>--prod</c>.</summary>
    public sealed record NpmPackage(string Name, string Version, string Licence, bool Production);
}
