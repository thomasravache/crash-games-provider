using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Apogeu.Security.Tests.Support;

/// <summary>
/// The signature formula of SPEC-0009 §4 written in the tests, independent of <c>OperatorRequestSignature</c>:
/// <c>HMAC-SHA256(secret, timestamp + "\n" + METHOD + "\n" + path + "\n" + hex(SHA256(body)))</c>, lowercase hex.
/// </summary>
internal static class ReferenceSignature
{
    public static string Compute(byte[] secret, long timestamp, string method, string path, byte[] body)
    {
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"{timestamp}\n{method.ToUpperInvariant()}\n{path}\n{Convert.ToHexStringLower(SHA256.HashData(body))}");
        return Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(message)));
    }
}
