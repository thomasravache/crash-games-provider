using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Support;

/// <summary>Calls of the casino BFF contract of SPEC-0043 §6 and the reading of its responses.</summary>
internal static class CasinoApi
{
    public const string SessionPath = "/casino/session";
    public const string MePath = "/casino/me";
    public const string LogoutPath = "/casino/logout";
    public const string SessionCookie = "casino_session";

    /// <summary>POST /casino/session with <c>{ nickname, currency }</c>.</summary>
    public static async Task<HttpResponseMessage> EnterAsync(HttpClient client, string nickname, string currency, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nickname"] = nickname,
            ["currency"] = currency,
        });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri(SessionPath, UriKind.Relative), content, ct).ConfigureAwait(false);
    }

    public static Task<HttpResponseMessage> MeAsync(HttpClient client, CancellationToken ct) =>
        client.GetAsync(new Uri(MePath, UriKind.Relative), ct);

    public static async Task<HttpResponseMessage> LogoutAsync(HttpClient client, CancellationToken ct)
    {
        using var content = new ByteArrayContent([]);
        return await client.PostAsync(new Uri(LogoutPath, UriKind.Relative), content, ct).ConfigureAwait(false);
    }

    /// <summary>A unique valid nickname (letters, digits and _; 3–20 characters).</summary>
    public static string NewNickname() => "it_" + Convert.ToHexStringLower(Guid.NewGuid().ToByteArray())[..12];

    /// <summary>The Set-Cookie header of the session cookie, or null when the response does not set it.</summary>
    public static SetCookieHeaderValue? SessionSetCookie(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? SetCookieHeaderValue.ParseList([.. values])
                .LastOrDefault(cookie => string.Equals(cookie.Name.Value, SessionCookie, StringComparison.Ordinal))
            : null;
    }

    /// <summary>Asserts a 200 JSON response and returns its root object.</summary>
    public static async Task<JsonElement> ReadOkJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}. Body: {text}");
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(text);
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        return json.RootElement.Clone();
    }

    /// <summary>Asserts the status and the error code at the root of the JSON body (<c>code</c>, as in SPEC-0009).</summary>
    public static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Assert.True(response.StatusCode == status, $"Expected {(int)status} {code}, got {(int)response.StatusCode}. Body: {text}");
        string? actual = null;
        try
        {
            using var json = JsonDocument.Parse(text);
            if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("code", out var value))
            {
                actual = value.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON: the assertion below reports the body.
        }

        Assert.True(string.Equals(code, actual, StringComparison.Ordinal), $"Expected error code {code}, got {actual ?? "<none>"}. Body: {text}");
    }
}
