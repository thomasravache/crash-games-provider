using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>
/// Signature of the calls to the operator wallet (public contract §3, scheme of SPEC-0009):
/// <c>hex(HMAC-SHA256(secret, timestamp + "\n" + METHOD + "\n" + path + "\n" + hex(SHA256(body))))</c>, lowercase.
/// </summary>
internal static class OperatorWalletSignature
{
    public const string KeyHeader = "X-Apogeu-Key";
    public const string TimestampHeader = "X-Apogeu-Timestamp";
    public const string SignatureHeader = "X-Apogeu-Signature";

    public static string Compute(string secret, long timestamp, string method, string path, ReadOnlySpan<byte> body)
    {
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"{timestamp}\n{method.ToUpperInvariant()}\n{path}\n{Convert.ToHexStringLower(SHA256.HashData(body))}");
        return Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(message)));
    }
}
