namespace Apogeu.BuildingBlocks.Security;

#pragma warning disable MA0025 // Scaffold of SPEC-0009 (no logic yet): implemented in the green phase.

/// <summary>
/// Signature of an operator request (SPEC-0009 §4): <c>HMAC-SHA256(secret, timestamp + "\n" + METHOD + "\n" + path + "\n" +
/// hex(SHA256(body)))</c>, as lowercase hex.
/// </summary>
public static class OperatorRequestSignature
{
    /// <summary>Computes the signature of a request as lowercase hex.</summary>
    public static string Compute(ReadOnlySpan<byte> secret, long timestamp, string method, string path, ReadOnlySpan<byte> body) =>
        throw new NotImplementedException();

    /// <summary>Whether <paramref name="signature"/> is the signature of the request, compared in constant time.</summary>
    public static bool Matches(ReadOnlySpan<byte> secret, long timestamp, string method, string path, ReadOnlySpan<byte> body, string signature) =>
        throw new NotImplementedException();
}
#pragma warning restore MA0025
