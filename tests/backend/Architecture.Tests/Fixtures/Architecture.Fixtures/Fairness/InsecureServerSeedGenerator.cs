namespace Apogeu.Fairness.Domain;

/// <summary>Known violation (UT-03): server seeds from System.Random, forbidden by ADR-0006.</summary>
public sealed class InsecureServerSeedGenerator
{
    private readonly Random _random = new();

    public byte[] NextSeed()
    {
        var seed = new byte[32];
        _random.NextBytes(seed);
        return seed;
    }
}
