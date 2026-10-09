using System.Net;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Integration;

/// <summary>IT-08: the launch rate limit per key and the security headers on every response (SPEC-0009 §5).</summary>
[Collection(SecurityDependencies.Name)]
public sealed class RateLimitAndHeadersTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const int PermittedPerMinute = 60;

    [Fact(DisplayName = "Dadas 61 chamadas de lançamento em 1 min com a mesma chave, então a 61ª recebe 429 RATE_LIMITED; a outra chave segue aceita")]
    [Trait("Spec", "SPEC-0009:IT-08")]
    [Trait("Category", "Integration")]
    public async Task GivenSixtyOneLaunchesInOneMinuteWithSameKeyThenTheLastIsRateLimited()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();

        for (var i = 1; i <= PermittedPerMinute; i++)
        {
            using var request = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.PrimaryKey, host.UnixNow());
            using var response = await client.SendAsync(request, ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"Launch {i} of {PermittedPerMinute} must be accepted, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");

            // The 60 calls spread over 59 s of the same minute.
            host.Clock.Advance(TimeSpan.FromMilliseconds(980));
        }

        using (var request = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.PrimaryKey, host.UnixNow()))
        using (var limited = await client.SendAsync(request, ct))
        {
            await ErrorBodies.AssertErrorAsync(limited, HttpStatusCode.TooManyRequests, "RATE_LIMITED", ct);
            AssertSecurityHeaders(limited, "429 RATE_LIMITED");
        }

        // Per key: the second key of the same operator is not affected.
        using (var request = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.SecondaryKey, host.UnixNow()))
        using (var other = await client.SendAsync(request, ct))
        {
            Assert.True(other.StatusCode == HttpStatusCode.OK, $"Another key must not be limited by the first one, got {(int)other.StatusCode}.");
        }
    }

    [Fact(DisplayName = "Dadas respostas de sucesso, de erro e de rota desconhecida, então toda resposta tem HSTS, X-Content-Type-Options: nosniff e CSP frame-ancestors")]
    [Trait("Spec", "SPEC-0009:IT-08")]
    [Trait("Category", "Integration")]
    public async Task GivenAnyResponseThenSecurityHeadersArePresent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();

        using (var request = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.PrimaryKey, host.UnixNow()))
        using (var ok = await client.SendAsync(request, ct))
        {
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            AssertSecurityHeaders(ok, "200 launch");
        }

        using (var request = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), TestApiKey.Generate(), host.UnixNow()))
        using (var unauthorized = await client.SendAsync(request, ct))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            AssertSecurityHeaders(unauthorized, "401 launch");
        }

        using (var session = OperatorApi.CreateSession("not-a-launch-token"))
        using (var rejected = await client.SendAsync(session, ct))
        {
            AssertSecurityHeaders(rejected, "401 session");
        }

        using (var unknown = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative), ct))
        {
            AssertSecurityHeaders(unknown, "unknown route");
        }
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response, string which)
    {
        Assert.True(
            response.Headers.TryGetValues("Strict-Transport-Security", out var hsts) && hsts.Any(v => v.Contains("max-age=", StringComparison.OrdinalIgnoreCase)),
            $"Missing Strict-Transport-Security on the {which} response.");
        Assert.True(
            response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff) && nosniff.Any(v => string.Equals(v, "nosniff", StringComparison.OrdinalIgnoreCase)),
            $"Missing X-Content-Type-Options: nosniff on the {which} response.");
        Assert.True(
            Values(response, "Content-Security-Policy").Any(v => v.Contains("frame-ancestors", StringComparison.OrdinalIgnoreCase)),
            $"Missing Content-Security-Policy with frame-ancestors on the {which} response.");
    }

    private static IEnumerable<string> Values(HttpResponseMessage response, string header) =>
        (response.Headers.TryGetValues(header, out var values) ? values : [])
        .Concat(response.Content.Headers.TryGetValues(header, out var content) ? content : []);

    private Task<SecurityHost> StartAsync(CancellationToken ct) =>
        SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions(), ct);
}
