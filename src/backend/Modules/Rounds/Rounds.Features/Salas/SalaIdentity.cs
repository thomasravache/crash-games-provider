using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Apogeu.Fairness.Contracts;

namespace Apogeu.Rounds.Features.Salas;

/// <summary>
/// Identity of a room (SPEC-0008 §4, emenda v2): a text id <c>^[a-z0-9-]{1,32}$</c> and, for the Fairness module, the UUID
/// v5 (RFC 9562) of that id under the fixed namespace of the Rounds module.
/// </summary>
public static partial class SalaIdentity
{
    /// <summary>Fixed UUID v5 namespace of the Rounds room ids; changing it would move every room to new seed chains.</summary>
    public static readonly Guid Namespace = new("8f3c2d71-5b0e-4a6f-9d1c-7e2b4a9c0f13");

    /// <summary>Whether <paramref name="salaId"/> is a valid room id (<c>^[a-z0-9-]{1,32}$</c>).</summary>
    public static bool IsValid(string? salaId) => salaId is not null && Pattern().IsMatch(salaId);

    /// <summary>The Fairness <see cref="SalaId"/> of the room: UUID v5 of its id under <see cref="Namespace"/>.</summary>
    public static SalaId ToFairness(string salaId)
    {
        ArgumentNullException.ThrowIfNull(salaId);

        var name = Encoding.UTF8.GetBytes(salaId);
        var data = new byte[16 + name.Length];
        Namespace.TryWriteBytes(data, bigEndian: true, out _);
        name.CopyTo(data, 16);

#pragma warning disable CA5350 // UUID v5 is defined over SHA-1 by RFC 9562; it names a room, it protects nothing.
        var hash = SHA1.HashData(data);
#pragma warning restore CA5350

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new SalaId(new Guid(hash.AsSpan(0, 16), bigEndian: true));
    }

    [GeneratedRegex("^[a-z0-9-]{1,32}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();
}
