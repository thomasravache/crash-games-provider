using System.Text;
using System.Text.Json;

namespace Apogeu.Security.Tests.Support;

/// <summary>Reads (never validates) the header and payload of a compact JWT, and forges variants of it for negative tests.</summary>
internal static class JwtParts
{
    public static JsonElement Header(string token) => Decode(Split(token)[0]);

    public static JsonElement Payload(string token) => Decode(Split(token)[1]);

    /// <summary>The same header and signature with the payload claim <paramref name="claim"/> replaced: the signature no longer matches.</summary>
    public static string WithClaim(string token, string claim, string value)
    {
        var parts = Split(token);
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Decode(parts[1]).GetRawText())!;
        payload[claim] = JsonSerializer.SerializeToElement(value);
        return parts[0] + "." + Encode(JsonSerializer.SerializeToUtf8Bytes(payload)) + "." + parts[2];
    }

    /// <summary>The same header and payload with the last character of the signature changed.</summary>
    public static string WithBrokenSignature(string token)
    {
        var parts = Split(token);
        var signature = parts[2];
        var last = signature[^1] == 'A' ? 'B' : 'A';
        return parts[0] + "." + parts[1] + "." + signature[..^1] + last;
    }

    /// <summary>An unsigned token (<c>alg: none</c>) with the same payload.</summary>
    public static string Unsigned(string token)
    {
        var parts = Split(token);
        return Encode("""{"alg":"none","typ":"JWT"}"""u8.ToArray()) + "." + parts[1] + ".";
    }

    private static string[] Split(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new Xunit.Sdk.XunitException($"Expected a compact JWT (3 parts), got {parts.Length} part(s).");
        }

        return parts;
    }

    private static JsonElement Decode(string part)
    {
        var base64 = part.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
        return document.RootElement.Clone();
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
