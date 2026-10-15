using System.Text.Json;

namespace Apogeu.Wallet.Tests.Support;

/// <summary>A request the operator test server received, with the exact body bytes (the signature is over them).</summary>
internal sealed record StubRequest(
    string Method,
    string Path,
    string? ContentType,
    string? KeyId,
    string? Timestamp,
    string? Signature,
    byte[] Body,
    DateTimeOffset ReceivedAt)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement.Clone();

    public string? TransactionId =>
        Json.ValueKind == JsonValueKind.Object && Json.TryGetProperty("transactionId", out var id) ? id.GetString() : null;
}
