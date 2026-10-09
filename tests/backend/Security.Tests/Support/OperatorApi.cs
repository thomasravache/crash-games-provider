using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Apogeu.BuildingBlocks.Security;

namespace Apogeu.Security.Tests.Support;

/// <summary>
/// Calls of the operator and player API of SPEC-0009 §6. The request signature is computed here, independently of the
/// production code, with the formula of §4, so a wrong implementation cannot make its own requests look valid.
/// </summary>
internal static class OperatorApi
{
    public const string LaunchPath = "/api/v1/operator/launch";
    public const string SessionsPath = "/api/v1/sessions";

    public static HttpRequestMessage SignedLaunch(byte[] body, TestApiKey key, long timestamp) =>
        SignedLaunch(body, key.Id, key.SecretBytes, timestamp);

    public static HttpRequestMessage SignedLaunch(byte[] body, string keyId, byte[] secret, long timestamp) =>
        Launch(body, keyId, timestamp, ReferenceSignature.Compute(secret, timestamp, "POST", LaunchPath, body));

    public static HttpRequestMessage Launch(byte[] body, string keyId, long timestamp, string signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(LaunchPath, UriKind.Relative))
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add(OperatorSignatureHeaders.KeyId, keyId);
        request.Headers.Add(OperatorSignatureHeaders.Timestamp, timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add(OperatorSignatureHeaders.Signature, signature);
        return request;
    }

    public static HttpRequestMessage CreateSession(string launchToken)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) { ["launchToken"] = launchToken }));
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(SessionsPath, UriKind.Relative)) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return request;
    }

    /// <summary>Launches a demo player and returns the launch token taken from the <c>lt</c> parameter of the launch URL.</summary>
    public static async Task<LaunchResult> LaunchDemoAsync(HttpClient client, SecurityHost host, TestApiKey key, CancellationToken ct, string? playerId = null)
    {
        using var request = SignedLaunch(LaunchBodies.Demo(playerId ?? LaunchBodies.NewPlayerId()), key, host.UnixNow());
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            throw new Xunit.Sdk.XunitException($"Expected 200 from POST {LaunchPath}, got {(int)response.StatusCode}: {text}");
        }

        using var json = JsonDocument.Parse(text);
        var launchUrl = new Uri(json.RootElement.GetProperty("launchUrl").GetString()!);
        return new LaunchResult(launchUrl, LaunchUrls.TokenOf(launchUrl), json.RootElement.GetProperty("expiresAt").GetDateTimeOffset(), text);
    }

    /// <summary>Exchanges a launch token for a session and returns the session token.</summary>
    public static async Task<string> CreateSessionTokenAsync(HttpClient client, string launchToken, CancellationToken ct)
    {
        using var request = CreateSession(launchToken);
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
            throw new Xunit.Sdk.XunitException($"Expected 200 from POST {SessionsPath}, got {(int)response.StatusCode}: {text}");
        }

        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("sessionToken").GetString()!;
    }
}
