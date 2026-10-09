using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;

namespace Apogeu.Fairness.Domain;

/// <summary>The hash chain of algorithm v1 (SPEC-0007 §4): h_{k+1} = SHA256(h_k) over the raw 32 bytes.</summary>
public static class SeedChain
{
    /// <summary>Distance between two stored checkpoints: reaching any link costs at most this many hashes minus one.</summary>
    public const int CheckpointInterval = 10_000;

    /// <summary>SHA256 of the seed: the next link h_{k+1}, and also the commitment of the round that uses the seed.</summary>
    public static Seed Next(Seed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        Span<byte> next = stackalloc byte[Seed.Length];
        SHA256.HashData(seed.AsSpan(), next);
        return new Seed(next);
    }

    /// <summary>The link h_{k+steps} reached by hashing h_k <paramref name="steps"/> times.</summary>
    public static Seed Advance(Seed link, int steps)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentOutOfRangeException.ThrowIfNegative(steps);

        Span<byte> current = stackalloc byte[Seed.Length];
        link.AsSpan().CopyTo(current);
        for (var i = 0; i < steps; i++)
        {
            SHA256.HashData(current, current);
        }

        var result = new Seed(current);
        CryptographicOperations.ZeroMemory(current);
        return result;
    }

    /// <summary>
    /// Walks the chain h_0..h_length from <paramref name="origin"/> (h_0): the checkpoints h_k with k a multiple of
    /// <see cref="CheckpointInterval"/> and k &lt; length (h_0 included), and the terminal hash h_length.
    /// </summary>
    public static ChainLayout Build(Seed origin, int length)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);

        var checkpoints = new List<ChainCheckpoint>((length / CheckpointInterval) + 1);
        Span<byte> current = stackalloc byte[Seed.Length];
        origin.AsSpan().CopyTo(current);
        for (var k = 0; k < length; k++)
        {
            if (k % CheckpointInterval == 0)
            {
                checkpoints.Add(new ChainCheckpoint(k, new Seed(current)));
            }

            SHA256.HashData(current, current);
        }

        var terminal = new Seed(current);
        CryptographicOperations.ZeroMemory(current);
        return new ChainLayout(checkpoints, terminal);
    }

    /// <summary>The index k of the link h_k that is the seed s_r of round <paramref name="round"/> (s_r = h_{N-r}).</summary>
    public static int LinkIndexOfRound(int length, int round)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(round, length);

        return length - round;
    }

    /// <summary>The checkpoint from which the link h_<paramref name="linkIndex"/> is reached.</summary>
    public static int CheckpointOf(int linkIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(linkIndex);

        return linkIndex - (linkIndex % CheckpointInterval);
    }
}
