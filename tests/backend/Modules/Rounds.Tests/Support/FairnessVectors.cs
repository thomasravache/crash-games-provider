using System.Text.Json;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>Loads the public vectors v1 of SPEC-0007, copied next to the test assembly.</summary>
internal static class FairnessVectors
{
    public static IReadOnlyList<FairnessVector> All()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vectors", "fairness-v1.json")));
        return [.. document.RootElement.EnumerateArray().Select(e => new FairnessVector(
            e.GetProperty("seed").GetString()!,
            e.GetProperty("previousSeed").GetString()!,
            e.GetProperty("salt").GetString()!,
            e.GetProperty("rtp").GetString()!,
            e.GetProperty("cap").GetString()!,
            e.GetProperty("crashPoint").GetString()!))];
    }
}
