namespace Apogeu.BuildingBlocks.Security;

#pragma warning disable MA0025 // Scaffold of SPEC-0009 (no logic yet): implemented in the green phase.

/// <summary>
/// Verifies an operator request against the secret of its key (SPEC-0009 §4): the timestamp must be within
/// <see cref="Window"/> of the injected clock and the signature must match. Replay detection is not done here.
/// </summary>
public sealed class OperatorRequestVerifier(TimeProvider timeProvider)
{
    /// <summary>Accepted distance between the request timestamp and the server clock, in either direction.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(300);

    public OperatorSignatureVerdict Verify(OperatorSignedRequest request, ReadOnlySpan<byte> secret) =>
        throw new NotImplementedException($"{nameof(OperatorRequestVerifier)} ({timeProvider}, {request}, {secret.Length})");

    /// <summary>Error code of the contract for a rejected verdict; <see langword="null"/> for <see cref="OperatorSignatureVerdict.Valid"/>.</summary>
    public static string? ErrorCodeOf(OperatorSignatureVerdict verdict) => throw new NotImplementedException();
}
#pragma warning restore MA0025
