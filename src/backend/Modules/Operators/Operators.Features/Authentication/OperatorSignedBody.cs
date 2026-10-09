namespace Apogeu.Operators.Features.Authentication;

/// <summary>
/// Request feature set by <see cref="OperatorSignatureAuthenticationHandler"/>: the exact body bytes the signature covers
/// (the endpoint reads these, never the stream again) and the signature itself in canonical form (lowercase hex), for replay detection.
/// </summary>
internal sealed class OperatorSignedBody(ReadOnlyMemory<byte> body, string signature)
{
    public ReadOnlyMemory<byte> Body { get; } = body;

    public string Signature { get; } = signature;

    /// <summary>Never the signature.</summary>
    public override string ToString() => $"{nameof(OperatorSignedBody)} {{ Length = {Body.Length} }}";
}
