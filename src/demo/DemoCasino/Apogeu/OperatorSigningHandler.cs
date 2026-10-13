using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace Apogeu.DemoCasino.Apogeu;

/// <summary>
/// Signs every request of the "Apogeu" client as the demo operator (SPEC-0009@1): X-Apogeu-Key, X-Apogeu-Timestamp
/// (Unix seconds) and X-Apogeu-Signature. Only the signature travels; the secret stays in this process.
/// </summary>
internal sealed class OperatorSigningHandler(IOptions<ApogeuOptions> options, TimeProvider time) : DelegatingHandler
{
    public const string KeyHeader = "X-Apogeu-Key";
    public const string TimestampHeader = "X-Apogeu-Timestamp";
    public const string SignatureHeader = "X-Apogeu-Signature";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        var uri = request.RequestUri ?? throw new InvalidOperationException("The Apogeu request has no URI.");
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var timestamp = time.GetUtcNow().ToUnixTimeSeconds();
        var signature = OperatorSignature.Compute(
            Encoding.UTF8.GetBytes(settings.OperatorSecret),
            timestamp,
            request.Method.Method,
            uri.AbsolutePath,
            body);

        request.Headers.Remove(KeyHeader);
        request.Headers.Remove(TimestampHeader);
        request.Headers.Remove(SignatureHeader);
        request.Headers.TryAddWithoutValidation(KeyHeader, settings.OperatorKeyId);
        request.Headers.TryAddWithoutValidation(TimestampHeader, timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation(SignatureHeader, signature);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
