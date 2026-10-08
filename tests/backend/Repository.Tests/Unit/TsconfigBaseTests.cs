using System.Collections.Immutable;
using System.Text.Json;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Repository.Tests.Unit;

public sealed class TsconfigBaseTests
{
    private static readonly string[] Apps = ["game-pipa", "backoffice", "demo-site"];
    private static readonly string[] Packages = ["contracts", "game-sdk", "game-ui", "scene-kit"];

    private static readonly JsonDocumentOptions JsoncOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [Theory(DisplayName = "Dado o tsconfig.base.json, quando lido, então a opção estrita é true")]
    [Trait("Spec", "SPEC-0002:UT-02")]
    [InlineData("strict")]
    [InlineData("noUncheckedIndexedAccess")]
    [InlineData("exactOptionalPropertyTypes")]
    public void GivenTsconfigBaseWhenReadThenStrictOptionIsTrue(string option)
    {
        using var document = ReadJsonc(BasePath());

        Assert.True(
            document.RootElement.TryGetProperty("compilerOptions", out var compilerOptions),
            "tsconfig.base.json has no \"compilerOptions\".");
        Assert.True(
            compilerOptions.TryGetProperty(option, out var value),
            $"tsconfig.base.json \"compilerOptions\" does not define \"{option}\".");
        Assert.Equal(JsonValueKind.True, value.ValueKind);
    }

    [Fact(DisplayName = "Dado o tsconfig.base.json, quando lido, então todos os apps e pacotes o estendem")]
    [Trait("Spec", "SPEC-0002:UT-02")]
    public void GivenTsconfigBaseWhenReadThenEveryAppAndPackageExtendsIt()
    {
        var basePath = BasePath();
        Assert.True(File.Exists(basePath), $"tsconfig.base.json was not found ({basePath}).");

        var expected = Apps.Select(app => Path.Combine("apps", app))
            .Concat(Packages.Select(package => Path.Combine("packages", package)))
            .ToList();

        var problems = new List<string>();
        foreach (var relativeDirectory in expected)
        {
            var tsconfig = RepositoryRoot.Combine("src", "frontend", relativeDirectory, "tsconfig.json");
            if (!File.Exists(tsconfig))
            {
                problems.Add($"{relativeDirectory}: tsconfig.json is missing");
            }
            else if (!ReachesBase(tsconfig, basePath, ImmutableHashSet<string>.Empty))
            {
                problems.Add($"{relativeDirectory}: tsconfig.json does not extend tsconfig.base.json");
            }
        }

        // Any other app/package that appears later must follow the rule as well.
        foreach (var root in new[] { "apps", "packages" })
        {
            var rootPath = RepositoryRoot.Combine("src", "frontend", root);
            if (!Directory.Exists(rootPath))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(rootPath))
            {
                var name = Path.GetFileName(directory);
                var relativeDirectory = Path.Combine(root, name);
                var tsconfig = Path.Combine(directory, "tsconfig.json");
                if (expected.Contains(relativeDirectory, StringComparer.Ordinal) || !File.Exists(tsconfig))
                {
                    continue;
                }

                if (!ReachesBase(tsconfig, basePath, ImmutableHashSet<string>.Empty))
                {
                    problems.Add($"{relativeDirectory}: tsconfig.json does not extend tsconfig.base.json");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static string BasePath() => RepositoryRoot.Combine("src", "frontend", "tsconfig.base.json");

    private static JsonDocument ReadJsonc(string path)
    {
        Assert.True(File.Exists(path), $"tsconfig.base.json was not found ({path}).");
        return JsonDocument.Parse(File.ReadAllText(path), JsoncOptions);
    }

    /// <summary>
    /// True when the tsconfig reaches the base through its "extends" chain, or when it only aggregates
    /// "references" (Vite style) and one of the referenced configs does.
    /// </summary>
    private static bool ReachesBase(string tsconfigPath, string basePath, ImmutableHashSet<string> chain)
    {
        if (string.IsNullOrEmpty(tsconfigPath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(tsconfigPath);
        if (string.Equals(fullPath, Path.GetFullPath(basePath), StringComparison.Ordinal))
        {
            return true;
        }

        if (!File.Exists(fullPath) || chain.Contains(fullPath))
        {
            return false;
        }

        chain = chain.Add(fullPath);

        using var document = ReadJsonc(fullPath);
        var directory = Path.GetDirectoryName(fullPath)!;

        if (document.RootElement.TryGetProperty("extends", out var extends))
        {
            var parents = extends.ValueKind == JsonValueKind.Array
                ? extends.EnumerateArray().Select(item => item.GetString())
                : [extends.GetString()];
            if (parents.Any(parent => ReachesBase(ResolveConfig(directory, parent), basePath, chain)))
            {
                return true;
            }
        }

        if (document.RootElement.TryGetProperty("references", out var references)
            && references.ValueKind == JsonValueKind.Array)
        {
            return references
                .EnumerateArray()
                .Select(item => item.TryGetProperty("path", out var path) ? path.GetString() : null)
                .Any(path => ReachesBase(ResolveConfig(directory, path), basePath, chain));
        }

        return false;
    }

    private static string ResolveConfig(string fromDirectory, string? reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return string.Empty;
        }

        var combined = Path.GetFullPath(Path.Combine(fromDirectory, reference));
        if (Directory.Exists(combined))
        {
            return Path.Combine(combined, "tsconfig.json");
        }

        return combined.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? combined : combined + ".json";
    }
}
