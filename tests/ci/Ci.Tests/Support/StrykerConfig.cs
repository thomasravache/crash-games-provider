using System.Text.Json;
using Apogeu.Repository.Tests.Support;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Reader of <c>stryker-config.json</c> (SPEC-0003, SPEC-0016): the fields the mutation gate depends on. Option names
/// follow the Stryker.NET 5 configuration file (<c>test-runner</c>, <c>coverage-analysis</c>, <c>thresholds.break</c>).
/// </summary>
public sealed class StrykerConfig
{
    public const string FileName = "stryker-config.json";

    /// <summary>Production project whose mutation score is the SPEC-0007 §7.6 evidence.</summary>
    public const string MutatedProjectSegment = "/src/backend/Modules/Fairness/Fairness.Domain/";

    /// <summary>Minimum mutation score of Fairness.Domain (SPEC-0007 §7.6).</summary>
    public const int MinimumScore = 85;

    private StrykerConfig(JsonElement section, string path)
    {
        Path = path;
        TestProjects = Strings(section, "test-projects");
        Mutate = Strings(section, "mutate");
        TestRunner = OptionalString(section, "test-runner");
        CoverageAnalysis = OptionalString(section, "coverage-analysis");
        Break = section.TryGetProperty("thresholds", out var thresholds)
            && thresholds.TryGetProperty("break", out var value)
            && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : null;
    }

    public string Path { get; }

    public IReadOnlyList<string> TestProjects { get; }

    public IReadOnlyList<string> Mutate { get; }

    /// <summary>Inclusion globs only (the <c>!…</c> exclusions removed).</summary>
    public IReadOnlyList<string> MutateIncludes => [.. Mutate.Where(glob => !glob.StartsWith('!'))];

    public string? TestRunner { get; }

    public string? CoverageAnalysis { get; }

    public int? Break { get; }

    public static StrykerConfig Load()
    {
        var path = RepositoryRoot.Combine(FileName);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return new StrykerConfig(document.RootElement.GetProperty("stryker-config").Clone(), path);
    }

    private static string[] Strings(JsonElement section, string name) =>
        section.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? [.. array.EnumerateArray().Select(item => item.GetString() ?? string.Empty)]
            : [];

    private static string? OptionalString(JsonElement section, string name) =>
        section.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
