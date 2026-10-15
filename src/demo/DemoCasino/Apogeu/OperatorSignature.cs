using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Apogeu.DemoCasino.Apogeu;

/// <summary>
/// The operator request signature of the public contract SPEC-0009@1 (§4):
/// <c>hex(HMAC-SHA256(secret, timestamp + "\n" + METHOD + "\n" + path + "\n" + hex(SHA256(body))))</c>, lowercase.
/// </summary>
internal static class OperatorSignature
{
    public static string Compute(ReadOnlySpan<byte> secret, long timestamp, string method, string path, ReadOnlySpan<byte> body)
    {
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"{timestamp}\n{method.ToUpperInvariant()}\n{path}\n{Convert.ToHexStringLower(SHA256.HashData(body))}");
        return Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(message)));
    }
}
