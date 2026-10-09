using System.Security.Cryptography;

namespace Apogeu.Fairness.Tests.Mutation;

/// <summary>Fixed inputs of the UT-02 tests and an independent walk of the hash chain (h_{k+1} = SHA256(h_k)).</summary>
internal static class MutationFixtures
{
    /// <summary>A fixed 32-byte origin h_0 (0x00, 0x07, 0x0e, …), distinct from all zeros.</summary>
    public static byte[] Origin() => [.. Enumerable.Range(0, 32).Select(i => (byte)(i * 7))];

    /// <summary>A second fixed 32-byte seed, unrelated to <see cref="Origin"/>.</summary>
    public static byte[] OtherSeed() => [.. Enumerable.Range(0, 32).Select(i => (byte)(255 - (i * 3)))];

    /// <summary>A fixed salt (16 bytes).</summary>
    public static byte[] SaltBytes() => [.. Enumerable.Range(0, 16).Select(i => (byte)(0xa0 + i))];

    /// <summary>The bytes after <paramref name="times"/> SHA-256 applications.</summary>
    public static byte[] HashTimes(byte[] bytes, int times)
    {
        var current = (byte[])bytes.Clone();
        for (var i = 0; i < times; i++)
        {
            current = SHA256.HashData(current);
        }

        return current;
    }

    /// <summary>The links h_0..h_length, with h_0 = <paramref name="origin"/>.</summary>
    public static byte[][] Walk(byte[] origin, int length)
    {
        var links = new byte[length + 1][];
        links[0] = (byte[])origin.Clone();
        for (var k = 1; k <= length; k++)
        {
            links[k] = SHA256.HashData(links[k - 1]);
        }

        return links;
    }
}
