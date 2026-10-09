using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Apogeu.BuildingBlocks.Security;

/// <summary>
/// Signature of an operator request (SPEC-0009 §4): <c>HMAC-SHA256(secret, timestamp + "\n" + METHOD + "\n" + path + "\n" +
/// hex(SHA256(body)))</c>, as lowercase hex.
/// </summary>
public static class OperatorRequestSignature
{
    private const int SignatureSizeInBytes = HMACSHA256.HashSizeInBytes;

    /// <summary>Computes the signature of a request as lowercase hex.</summary>
    public static string Compute(ReadOnlySpan<byte> secret, long timestamp, string method, string path, ReadOnlySpan<byte> body)
    {
        Span<byte> signature = stackalloc byte[SignatureSizeInBytes];
        Sign(secret, timestamp, method, path, body, signature);
        return Convert.ToHexStringLower(signature);
    }

    /// <summary>Whether <paramref name="signature"/> is the signature of the request, compared in constant time.</summary>
    public static bool Matches(ReadOnlySpan<byte> secret, long timestamp, string method, string path, ReadOnlySpan<byte> body, string signature)
    {
        if (signature is not { Length: SignatureSizeInBytes * 2 })
        {
            return false;
        }

        Span<byte> presented = stackalloc byte[SignatureSizeInBytes];
        if (Convert.FromHexString(signature, presented, out _, out var written) != System.Buffers.OperationStatus.Done
            || written != SignatureSizeInBytes)
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[SignatureSizeInBytes];
        Sign(secret, timestamp, method, path, body, expected);
        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }

    private static void Sign(ReadOnlySpan<byte> secret, long timestamp, string method, string path, ReadOnlySpan<byte> body, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);

        Span<byte> bodyHash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(body, bodyHash);
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"{timestamp}\n{method.ToUpperInvariant()}\n{path}\n{Convert.ToHexStringLower(bodyHash)}");
        HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(message), destination);
    }
}
