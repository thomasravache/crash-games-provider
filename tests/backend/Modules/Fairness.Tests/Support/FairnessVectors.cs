using System.Text.Json;
using Xunit;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>Loads the public vectors v1 and their schema, copied next to the test assembly.</summary>
internal static class FairnessVectors
{
    public static string VectorsPath => Path.Combine(AppContext.BaseDirectory, "vectors", "fairness-v1.json");

    public static string SchemaPath => Path.Combine(AppContext.BaseDirectory, "vectors", "fairness-v1.schema.json");

    public static IReadOnlyList<FairnessVector> All()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(VectorsPath));
        return [.. document.RootElement.GetProperty("vectors").EnumerateArray().Select(Read)];
    }

    /// <summary>The vectors as theory rows (description, seed, previousSeed, salt, rtp, cap, crashPoint).</summary>
    public static TheoryData<string, string, string, string, string, string, string> Rows()
    {
        var rows = new TheoryData<string, string, string, string, string, string, string>();
        foreach (var vector in All())
        {
            rows.Add(vector.Description, vector.Seed, vector.PreviousSeed, vector.Salt, vector.Rtp, vector.Cap, vector.CrashPoint);
        }

        return rows;
    }

    private static FairnessVector Read(JsonElement element) =>
        new(
            element.TryGetProperty("description", out var description) ? description.GetString() ?? string.Empty : string.Empty,
            element.GetProperty("seed").GetString()!,
            element.GetProperty("previousSeed").GetString()!,
            element.GetProperty("salt").GetString()!,
            element.GetProperty("rtp").GetString()!,
            element.GetProperty("cap").GetString()!,
            element.GetProperty("crashPoint").GetString()!);
}
