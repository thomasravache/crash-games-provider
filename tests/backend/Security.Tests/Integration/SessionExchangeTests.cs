using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Integration;

/// <summary>IT-04: exchanging a launch token for a player session (SPEC-0009 §4 and §6) on the real Host.</summary>
[Collection(SecurityDependencies.Name)]
public sealed class SessionExchangeTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dado um launchToken, quando trocado, então 200 com JWT assinado contendo sub, op, sala, cur e mode, válido por 8 h")]
    [Trait("Spec", "SPEC-0009:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenLaunchTokenWhenExchangedThenOkWithSignedSessionJwt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var launch = await OperatorApi.LaunchDemoAsync(client, host, host.PrimaryKey, ct, playerId: "player-it04");
        var exchangedAt = host.Clock.GetUtcNow();

        using var request = OperatorApi.CreateSession(launch.LaunchToken);
        using var response = await client.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        Assert.Equal("demo", root.GetProperty("salaId").GetString());
        Assert.Equal("BRL", root.GetProperty("currency").GetString());
        Assert.Equal("demo", root.GetProperty("mode").GetString());
        Assert.Equal("Pipeiro", root.GetProperty("player").GetProperty("nickname").GetString());
        Assert.Equal(exchangedAt.AddHours(8), root.GetProperty("expiresAt").GetDateTimeOffset());

        var sessionToken = root.GetProperty("sessionToken").GetString()!;
        var header = JwtParts.Header(sessionToken);
        var alg = header.GetProperty("alg").GetString();
        Assert.False(string.IsNullOrEmpty(alg) || string.Equals(alg, "none", StringComparison.OrdinalIgnoreCase), $"The session must be signed, alg was '{alg}'.");

        var payload = JwtParts.Payload(sessionToken);
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("sub").GetString()), "The session needs a pseudonymous subject (sub).");
        Assert.Equal("demo", payload.GetProperty("op").GetString());
        Assert.Equal("demo", payload.GetProperty("sala").GetString());
        Assert.Equal("BRL", payload.GetProperty("cur").GetString());
        Assert.Equal("demo", payload.GetProperty("mode").GetString());
        Assert.Equal(exchangedAt.AddHours(8).ToUnixTimeSeconds(), payload.GetProperty("exp").GetInt64());

        // Only the pseudonymous id, optional nickname, currency and language (SPEC-0009 §5): no launch token in the session.
        Assert.DoesNotContain(launch.LaunchToken, payload.GetRawText(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Dado um launchToken já trocado, quando reutilizado, então 401 LAUNCH_TOKEN_USED")]
    [Trait("Spec", "SPEC-0009:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenExchangedLaunchTokenWhenReusedThenLaunchTokenUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var launch = await OperatorApi.LaunchDemoAsync(client, host, host.PrimaryKey, ct);
        await OperatorApi.CreateSessionTokenAsync(client, launch.LaunchToken, ct);

        using var reuse = OperatorApi.CreateSession(launch.LaunchToken);
        using var response = await client.SendAsync(reuse, ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "LAUNCH_TOKEN_USED", ct);
    }

    [Fact(DisplayName = "Dados dois pedidos simultâneos com o mesmo launchToken, então exatamente um recebe a sessão")]
    [Trait("Spec", "SPEC-0009:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenConcurrentExchangesOfSameLaunchTokenThenExactlyOneSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var launch = await OperatorApi.LaunchDemoAsync(client, host, host.PrimaryKey, ct);

        var statuses = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var request = OperatorApi.CreateSession(launch.LaunchToken);
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            return response.StatusCode;
        }));

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.All(statuses.Where(s => s != HttpStatusCode.OK), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
    }

    [Fact(DisplayName = "Dado um launchToken, quando trocado após 61 s, então 401 LAUNCH_TOKEN_EXPIRED")]
    [Trait("Spec", "SPEC-0009:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenLaunchTokenWhenExchangedAfter61SecondsThenLaunchTokenExpired()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var launch = await OperatorApi.LaunchDemoAsync(client, host, host.PrimaryKey, ct);

        host.Clock.Advance(TimeSpan.FromSeconds(61));
        using var request = OperatorApi.CreateSession(launch.LaunchToken);
        using var response = await client.SendAsync(request, ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "LAUNCH_TOKEN_EXPIRED", ct);
    }

    [Fact(DisplayName = "Dado um launchToken aleatório, quando trocado, então 401 LAUNCH_TOKEN_INVALID")]
    [Trait("Spec", "SPEC-0009:IT-04")]
    [Trait("Category", "Integration")]
    public async Task GivenRandomLaunchTokenWhenExchangedThenLaunchTokenInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        using var request = OperatorApi.CreateSession(random);
        using var response = await client.SendAsync(request, ct);

        await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.Unauthorized, "LAUNCH_TOKEN_INVALID", ct);
    }

    private Task<SecurityHost> StartAsync(CancellationToken ct) =>
        SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions(), ct);
}
