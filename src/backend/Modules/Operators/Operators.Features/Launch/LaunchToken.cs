namespace Apogeu.Operators.Features.Launch;

#pragma warning disable MA0025 // Scaffold of SPEC-0009 (no logic yet): implemented in the green phase.

/// <summary>
/// Single-use launch token (SPEC-0009 §4): 32 random bytes, URL-safe text. Only <see cref="Hash"/> is stored; the value
/// itself goes to the operator once, inside the launch URL.
/// </summary>
public sealed class LaunchToken
{
    /// <summary>Number of random bytes of a token.</summary>
    public const int SizeInBytes = 32;

    private LaunchToken()
    {
    }

    /// <summary>The token as sent in <c>lt</c>; never logged nor stored.</summary>
    public string Value => throw new NotImplementedException();

    /// <summary>Hash of <see cref="Value"/> (see <see cref="HashOf"/>), the only form that is stored.</summary>
    public string Hash => throw new NotImplementedException();

    /// <summary>Creates a token from a cryptographic random source.</summary>
    public static LaunchToken Generate() => throw new NotImplementedException();

    /// <summary>Hash under which a presented token value is looked up.</summary>
    public static string HashOf(string value) => throw new NotImplementedException();

    /// <summary>Never the token value, so that a token passed to a logger or an exception message does not leak.</summary>
    public override string ToString() => throw new NotImplementedException();
}
#pragma warning restore MA0025
