using System.Net;
using System.Text.Json;
using Xunit;

namespace Apogeu.Security.Tests.Support;

/// <summary>
/// Error responses of the contract (SPEC-0009 §6): the status and the error code, read from the <c>code</c> property at
/// the root of the JSON body (a plain object or a ProblemDetails extension serialize the same way).
/// </summary>
internal static class ErrorBodies
{
    public static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Assert.True(
            response.StatusCode == status,
            $"Expected {(int)status} {code}, got {(int)response.StatusCode}. Body: {text}");
        Assert.Equal(code, CodeOf(text));
    }

    public static string? CodeOf(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("code", out var code)
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
