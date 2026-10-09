namespace Apogeu.BuildingBlocks.Security;

/// <summary>
/// Verifies an operator request against the secret of its key (SPEC-0009 §4): the timestamp must be within
/// <see cref="Window"/> of the injected clock and the signature must match. Replay detection is not done here.
/// </summary>
public sealed class OperatorRequestVerifier(TimeProvider timeProvider)
{
    /// <summary>Accepted distance between the request timestamp and the server clock, in either direction.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(300);

    private static readonly long WindowSeconds = (long)Window.TotalSeconds;

    public OperatorSignatureVerdict Verify(OperatorSignedRequest request, ReadOnlySpan<byte> secret)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (request.Timestamp < now - WindowSeconds || request.Timestamp > now + WindowSeconds)
        {
            return OperatorSignatureVerdict.TimestampOutOfWindow;
        }

        return OperatorRequestSignature.Matches(secret, request.Timestamp, request.Method, request.Path, request.Body.Span, request.Signature)
            ? OperatorSignatureVerdict.Valid
            : OperatorSignatureVerdict.InvalidSignature;
    }

    /// <summary>Error code of the contract for a rejected verdict; <see langword="null"/> for <see cref="OperatorSignatureVerdict.Valid"/>.</summary>
    public static string? ErrorCodeOf(OperatorSignatureVerdict verdict) => verdict switch
    {
        OperatorSignatureVerdict.Valid => null,
        OperatorSignatureVerdict.InvalidSignature => SecurityErrorCodes.InvalidSignature,
        OperatorSignatureVerdict.TimestampOutOfWindow => SecurityErrorCodes.TimestampOutOfWindow,
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Unknown verdict."),
    };
}
