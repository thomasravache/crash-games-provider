using System.Text.Json;
using System.Xml.Linq;

namespace Apogeu.Architecture.Tests.Rules;

/// <summary>
/// ADR-0002 at package level: finds NuGet package ids that start with MediatR, AutoMapper or MassTransit
/// (case-insensitive) in Directory.Packages.props and in every packages.lock.json under a repository root.
/// </summary>
public static class PackageReferenceScanner
{
    private static readonly string[] SkippedDirectories = ["bin", "obj", "node_modules", ".git", "artifacts"];
    private static readonly string[] PackageElements = ["PackageVersion", "PackageReference", "GlobalPackageReference"];

    /// <summary>Forbidden package id prefixes (ADR-0002, commercial licences).</summary>
    public static IReadOnlyList<string> ForbiddenPackageIdPrefixes { get; } = ["MediatR", "AutoMapper", "MassTransit"];

    /// <summary>
    /// Every forbidden package found under <paramref name="repositoryRoot"/>, one entry per occurrence, formatted as
    /// <c>&lt;relative file path&gt;: &lt;package id&gt;</c>. Empty when the repository is compliant.
    /// </summary>
    public static IReadOnlyList<string> FindForbiddenPackages(string repositoryRoot) =>
        [
            .. Manifests(repositoryRoot)
                .SelectMany(file => PackageIds(file)
                    .Where(IsForbidden)
                    .Select(id => $"{Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/')}: {id}"))
                .Order(StringComparer.Ordinal),
        ];

    private static bool IsForbidden(string packageId) =>
        ForbiddenPackageIdPrefixes.Any(prefix => packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> PackageIds(string file) =>
        string.Equals(Path.GetFileName(file), "packages.lock.json", StringComparison.OrdinalIgnoreCase)
            ? LockFilePackageIds(file)
            : XDocument.Load(file)
                .Descendants()
                .Where(element => PackageElements.Contains(element.Name.LocalName, StringComparer.Ordinal))
                .Select(element => (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update"))
                .OfType<string>();

    /// <summary>Package ids of a NuGet lock file: <c>dependencies.&lt;framework&gt;.&lt;id&gt;</c>.</summary>
    private static List<string> LockFilePackageIds(string file)
    {
        using var lockFile = JsonDocument.Parse(File.ReadAllText(file));
        return lockFile.RootElement.TryGetProperty("dependencies", out var frameworks)
            ? [.. frameworks.EnumerateObject().SelectMany(framework => framework.Value.EnumerateObject().Select(package => package.Name))]
            : [];
    }

    private static IEnumerable<string> Manifests(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(file);
            if (name.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
                || name.Equals("packages.lock.json", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.Ordinal))
            {
                continue;
            }

            foreach (var file in Manifests(child))
            {
                yield return file;
            }
        }
    }
}
