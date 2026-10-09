namespace Apogeu.BuildingBlocks.Security;

/// <summary>Headers of a request signed by an operator system (SPEC-0009 §4, ADR-0015).</summary>
public static class OperatorSignatureHeaders
{
    /// <summary>Identifier of the operator API key that signed the request.</summary>
    public const string KeyId = "X-Apogeu-Key";

    /// <summary>Unix time in seconds when the request was signed.</summary>
    public const string Timestamp = "X-Apogeu-Timestamp";

    /// <summary>Lowercase hex of the HMAC-SHA256 signature.</summary>
    public const string Signature = "X-Apogeu-Signature";
}
