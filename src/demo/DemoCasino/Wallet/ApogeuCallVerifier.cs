using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Apogeu.DemoCasino.Apogeu;
using Microsoft.Extensions.Options;

namespace Apogeu.DemoCasino.Wallet;

/// <summary>
/// Verifies the Apogeu signature of a call to the casino wallet (public contract §3, SPEC-0047 Emenda v1): the key is the
/// demo operator key the casino already holds (<c>Apogeu:OperatorKeyId</c>/<c>OperatorSecret</c>), the signature is
/// <see cref="OperatorSignature"/> over the timestamp, the method, the path as sent and the exact body bytes, compared in
/// constant time, and the timestamp must be within ±300 s of the <see cref="TimeProvider"/>. The secret never leaves the
/// process.
/// </summary>
internal sealed class ApogeuCallVerifier(IOptions<ApogeuOptions> options, TimeProvider time)
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(300);

    public bool IsValid(HttpRequest request, ReadOnlySpan<byte> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        var keyId = request.Headers[OperatorSigningHandler.KeyHeader].ToString();
        var timestampText = request.Headers[OperatorSigningHandler.TimestampHeader].ToString();
        var signature = request.Headers[OperatorSigningHandler.SignatureHeader].ToString();
        if (keyId.Length == 0 || signature.Length == 0
            || !long.TryParse(timestampText, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
        {
            return false;
        }

        var keyMatches = FixedTimeEquals(keyId, settings.OperatorKeyId);
        var now = time.GetUtcNow().ToUnixTimeSeconds();
        var inWindow = Math.Abs(now - timestamp) <= (long)Tolerance.TotalSeconds;
        var expected = OperatorSignature.Compute(
            Encoding.UTF8.GetBytes(settings.OperatorSecret),
            timestamp,
            request.Method,
            request.PathBase.Add(request.Path).Value ?? string.Empty,
            body);
        var signatureMatches = FixedTimeEquals(signature, expected);
        return keyMatches & inWindow & signatureMatches;
    }

    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
