using System.Text.Json;

namespace Apogeu.Ci.Tests.Support;

/// <summary>
/// Reader of the Stryker JSON report (mutation-testing-elements schema): per file, the mutants with their status and
/// the tests that cover and kill them.
/// </summary>
public sealed class MutationReport
{
    private MutationReport(IReadOnlyDictionary<string, IReadOnlyList<Mutant>> files) => Files = files;

    /// <summary>Mutants by source file path, as written by Stryker.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Mutant>> Files { get; }

    public IEnumerable<Mutant> AllMutants => Files.Values.SelectMany(mutants => mutants);

    /// <summary>Stryker's mutation score: detected (Killed, Timeout) over valid (detected, Survived, NoCoverage), in %.</summary>
    public double Score => ScoreOf(AllMutants);

    public static MutationReport Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var files = new Dictionary<string, IReadOnlyList<Mutant>>(StringComparer.Ordinal);
        foreach (var file in document.RootElement.GetProperty("files").EnumerateObject())
        {
            files[file.Name] = [.. file.Value.GetProperty("mutants").EnumerateArray().Select(Mutant.From)];
        }

        return new MutationReport(files);
    }

    public static double ScoreOf(IEnumerable<Mutant> mutants)
    {
        var list = mutants.ToList();
        var detected = list.Count(mutant => mutant.IsDetected);
        var valid = list.Count(mutant => mutant.IsDetected || mutant.Status is "Survived" or "NoCoverage");
        return valid == 0 ? 0 : 100.0 * detected / valid;
    }

    /// <summary>Mutants of the file whose path ends with <paramref name="fileName"/>.</summary>
    public IReadOnlyList<Mutant> MutantsOf(string fileName) =>
        [.. Files
            .Where(file => file.Key.Replace('\\', '/').EndsWith("/" + fileName, StringComparison.Ordinal))
            .SelectMany(file => file.Value)];

    public sealed record Mutant(string Id, string MutatorName, string Status, int Line, IReadOnlyList<string> CoveredBy, IReadOnlyList<string> KilledBy)
    {
        public bool IsDetected => Status is "Killed" or "Timeout";

        public bool IsCovered => CoveredBy.Count > 0 || KilledBy.Count > 0 || IsDetected || string.Equals(Status, "Survived", StringComparison.Ordinal);

        public static Mutant From(JsonElement element) => new(
            element.TryGetProperty("id", out var id) ? id.ToString() : string.Empty,
            element.TryGetProperty("mutatorName", out var mutator) ? mutator.GetString() ?? string.Empty : string.Empty,
            element.GetProperty("status").GetString() ?? string.Empty,
            element.TryGetProperty("location", out var location) ? location.GetProperty("start").GetProperty("line").GetInt32() : 0,
            Ids(element, "coveredBy"),
            Ids(element, "killedBy"));

        private static string[] Ids(JsonElement element, string name) =>
            element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
                ? [.. array.EnumerateArray().Select(item => item.GetString() ?? string.Empty)]
                : [];
    }
}
