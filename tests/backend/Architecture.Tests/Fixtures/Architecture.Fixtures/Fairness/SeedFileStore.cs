namespace Apogeu.Fairness.Domain;

/// <summary>Known violation (IT-01 guard): Fairness.Domain does file I/O (ADR-0001).</summary>
public static class SeedFileStore
{
    public static byte[] Load(string path) => File.ReadAllBytes(path);
}
