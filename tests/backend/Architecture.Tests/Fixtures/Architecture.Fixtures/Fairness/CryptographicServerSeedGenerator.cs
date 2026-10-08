using System.Security.Cryptography;

namespace Apogeu.Fairness.Domain;

/// <summary>Compliant seed source (UT-03 control): RandomNumberGenerator.</summary>
public static class CryptographicServerSeedGenerator
{
    public static byte[] NextSeed() => RandomNumberGenerator.GetBytes(32);
}
