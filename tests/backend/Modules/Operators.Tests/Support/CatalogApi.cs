using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Apogeu.BuildingBlocks.Security;
using Apogeu.Security.Tests.Support;

namespace Apogeu.Operators.Tests.Support;

/// <summary>
/// Calls of the operator API used by SPEC-0020, signed here with the formula of SPEC-0009 §4 (<see cref="ReferenceSignature"/>),
/// independently of the production code.
/// </summary>
internal static class CatalogApi
{
    public const string LaunchPath = "/api/v1/operator/launch";
    public const string GamesPath = "/api/v1/games";
    public const string SessionsPath = "/api/v1/sessions";

    /// <summary>
    /// A demo launch body (SPEC-0009 §6) with the given game and currency, and the <paramref name="nickname"/> (null: sent as
    /// null), as the exact bytes signed and sent.
    /// </summary>
    public static byte[] LaunchBody(string gameCode, string currency, string? nickname = "Pipeiro") =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playerId"] = "player-" + Guid.NewGuid().ToString("N"),
            ["nickname"] = nickname,
            ["currency"] = currency,
            ["gameCode"] = gameCode,
            ["mode"] = "demo",
            ["language"] = "pt-BR",
        }));

    public static HttpRequestMessage SignedLaunch(byte[] body, TestApiKey key, long timestamp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(LaunchPath, UriKind.Relative)) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        Sign(request, key, timestamp, "POST", LaunchPath, body);
        return request;
    }

    /// <summary><c>GET /api/v1/games</c> signed with an empty body.</summary>
    public static HttpRequestMessage SignedListGames(TestApiKey key, long timestamp)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(GamesPath, UriKind.Relative));
        Sign(request, key, timestamp, "GET", GamesPath, []);
        return request;
    }

    public static HttpRequestMessage UnsignedListGames() => new(HttpMethod.Get, new Uri(GamesPath, UriKind.Relative));

    private static void Sign(HttpRequestMessage request, TestApiKey key, long timestamp, string method, string path, byte[] body)
    {
        request.Headers.Add(OperatorSignatureHeaders.KeyId, key.Id);
        request.Headers.Add(OperatorSignatureHeaders.Timestamp, timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.Add(OperatorSignatureHeaders.Signature, ReferenceSignature.Compute(key.SecretBytes, timestamp, method, path, body));
    }
}
