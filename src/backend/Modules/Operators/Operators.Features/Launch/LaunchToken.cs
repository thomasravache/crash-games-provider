using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Apogeu.Operators.Features.Launch;

/// <summary>
/// Single-use launch token (SPEC-0009 §4): 32 random bytes, URL-safe text. Only <see cref="Hash"/> is stored; the value
/// itself goes to the operator once, inside the launch URL.
/// </summary>
public sealed class LaunchToken
{
    /// <summary>Number of random bytes of a token.</summary>
    public const int SizeInBytes = 32;

    private LaunchToken(string value)
    {
        Value = value;
        Hash = HashOf(value);
    }

    /// <summary>The token as sent in <c>lt</c>; never logged nor stored.</summary>
    public string Value { get; }

    /// <summary>Hash of <see cref="Value"/> (see <see cref="HashOf"/>), the only form that is stored.</summary>
    public string Hash { get; }

    /// <summary>Creates a token from a cryptographic random source.</summary>
    public static LaunchToken Generate() => new(WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SizeInBytes)));

    /// <summary>Hash under which a presented token value is looked up: lowercase hex of SHA-256 of its UTF-8 text.</summary>
    public static string HashOf(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>Never the token value, so that a token passed to a logger or an exception message does not leak.</summary>
    public override string ToString() => nameof(LaunchToken) + "(***)";
}
