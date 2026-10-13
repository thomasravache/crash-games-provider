using System.Net;
using System.Text;
using System.Text.Json;
using Apogeu.Operators.Tests.Support;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Operators.Tests.Integration;

/// <summary>
/// UT-03, Operators side (SPEC-0025 §6, Emenda v2): the player session issued for a launch carries the optional claim
/// <c>nick</c> with the nickname of the launch, and no such claim when the launch had none. Runs through the real issue path
/// (signed launch → <c>POST /api/v1/sessions</c>) on the Host, the only public entry of the session tokens.
/// </summary>
[Collection(OperatorsApiDependencies.Name)]
public sealed class SessionNicknameClaimTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dado um lançamento com apelido \"Marcos\" e outro sem apelido, quando a sessão do jogador é criada, então o token traz a claim nick = \"Marcos\" no primeiro e nenhuma claim nick no segundo")]
    [Trait("Spec", "SPEC-0025:UT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenLaunchWithAndWithoutNicknameWhenSessionIsCreatedThenNickClaimOnlyWhenPresent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await OperatorsApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        using var client = host.CreateClient();

        var withNickname = JwtParts.Payload(await SessionTokenAsync(host, client, "Marcos", ct));
        var withoutNickname = JwtParts.Payload(await SessionTokenAsync(host, client, null, ct));

        Assert.True(withNickname.TryGetProperty("nick", out var nick), $"The session of a launch with a nickname carries the claim nick: {withNickname.GetRawText()}");
        Assert.Equal("Marcos", nick.GetString());
        Assert.False(withoutNickname.TryGetProperty("nick", out _), $"The session of a launch without a nickname has no claim nick: {withoutNickname.GetRawText()}");

        // The other claims of SPEC-0009 §4 stay as they were.
        foreach (var payload in (JsonElement[])[withNickname, withoutNickname])
        {
            foreach (var claim in (string[])["sub", "op", "sala", "cur", "mode"])
            {
                Assert.True(payload.TryGetProperty(claim, out var value) && !string.IsNullOrEmpty(value.GetString()), $"The session keeps the claim {claim}: {payload.GetRawText()}");
            }
        }
    }

    /// <summary>A signed demo launch of <c>pipa</c> in BRL with <paramref name="nickname"/>, exchanged for a session token.</summary>
    private static async Task<string> SessionTokenAsync(OperatorsApiHost host, HttpClient client, string? nickname, CancellationToken ct)
    {
        string launchToken;
        using (var launch = CatalogApi.SignedLaunch(CatalogApi.LaunchBody("pipa", "BRL", nickname), host.Key, host.UnixNow()))
        using (var launched = await client.SendAsync(launch, ct).ConfigureAwait(false))
        {
            var text = await launched.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            Assert.True(launched.StatusCode == HttpStatusCode.OK, $"Expected 200 from POST {CatalogApi.LaunchPath}, got {(int)launched.StatusCode}: {text}");
            using var json = JsonDocument.Parse(text);
            launchToken = LaunchUrls.TokenOf(new Uri(json.RootElement.GetProperty("launchUrl").GetString()!));
        }

        var body = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) { ["launchToken"] = launchToken });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var session = await client.PostAsync(new Uri(CatalogApi.SessionsPath, UriKind.Relative), content, ct).ConfigureAwait(false);
        var sessionText = await session.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Assert.True(session.StatusCode == HttpStatusCode.OK, $"Expected 200 from POST {CatalogApi.SessionsPath}, got {(int)session.StatusCode}: {sessionText}");
        using var sessionJson = JsonDocument.Parse(sessionText);
        return sessionJson.RootElement.GetProperty("sessionToken").GetString()!;
    }
}
