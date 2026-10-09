using System.Net;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Integration;

/// <summary>IT-09: <c>GET /dev/launch-demo</c> exists only in Development (SPEC-0009 §6).</summary>
[Collection(SecurityDependencies.Name)]
public sealed class DevelopmentRouteTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const string DevLaunchPath = "/dev/launch-demo";

    [Fact(DisplayName = "Dado o Host em Production, quando GET /dev/launch-demo, então 404 (e a API de lançamento continua mapeada)")]
    [Trait("Spec", "SPEC-0009:IT-09")]
    [Trait("Category", "Integration")]
    public async Task GivenProductionWhenGettingDevLaunchThenNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions { Environment = "Production" }, ct);
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri(DevLaunchPath, UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.Location);

        // Not vacuous: this is the real Host in Production with the security API mapped; only the dev route is absent.
        using var request = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), TestApiKey.Generate(), host.UnixNow());
        using var launch = await client.SendAsync(request, ct);
        await ErrorBodies.AssertErrorAsync(launch, HttpStatusCode.Unauthorized, "UNKNOWN_KEY", ct);
    }

    [Fact(DisplayName = "Dado o Host em Development, quando GET /dev/launch-demo, então 302 para uma launchUrl válida (trocável por sessão)")]
    [Trait("Spec", "SPEC-0009:IT-09")]
    [Trait("Category", "Integration")]
    public async Task GivenDevelopmentWhenGettingDevLaunchThenRedirectsToValidLaunchUrl()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions(), ct);
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri(DevLaunchPath, UriKind.Relative), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.True(location.IsAbsoluteUri, $"Expected an absolute launch URL, got {location}.");
        Assert.Equal(SecurityHost.GameBaseUrl.GetLeftPart(UriPartial.Authority), location.GetLeftPart(UriPartial.Authority));
        Assert.Equal("/", location.AbsolutePath);

        // Valid: the token in it opens a demo session.
        var sessionToken = await OperatorApi.CreateSessionTokenAsync(client, LaunchUrls.TokenOf(location), ct);
        Assert.Equal("demo", JwtParts.Payload(sessionToken).GetProperty("mode").GetString());
    }
}
