namespace Apogeu.Operators.Features.Authentication;

/// <summary>An active API key of an operator. The secret is used as its UTF-8 bytes; it is never logged.</summary>
internal sealed record OperatorApiKey(string Id, string OperatorId, byte[] Secret)
{
    /// <summary>Never the secret.</summary>
    public override string ToString() => $"{nameof(OperatorApiKey)} {{ Id = {Id}, OperatorId = {OperatorId} }}";
}
