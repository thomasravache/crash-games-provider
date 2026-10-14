using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.TestKit.Performance;
using Xunit;

namespace Apogeu.Security.Tests.Integration;

/// <summary>
/// IT-01: a correctly signed launch of the demo operator returns a launch URL, with p99 ≤ 100 ms over 200 calls on real
/// PostgreSQL and Redis (SPEC-0009 §5). Timed, so it runs in a collection without parallelism.
/// </summary>
[Collection(TimedSecurityDependencies.Name)]
public sealed class LaunchPerformanceTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const int WarmUps = 20;
    private const int Samples = 200;

    // The launch rate limit is 60 per minute per key (IT-08). Its window follows the injected clock (ADR-0009), so the
    // test moves the fake clock one full window forward after every batch, outside the measured region.
    private const int BatchPerWindow = 50;

    // 100 ms on the reference machine, × the machine factor elsewhere (SPEC-0052).
    private static readonly (TimeSpan Budget, string Source) Budget =
        PerformanceBudget.Resolve(TimeSpan.FromMilliseconds(100), Environment.GetEnvironmentVariable);

    [Fact(DisplayName = "Dado o operador demo e Postgres/Redis reais, quando o lançamento é assinado corretamente, então 200 com launchUrl")]
    [Trait("Spec", "SPEC-0009:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenDemoOperatorWhenLaunchIsCorrectlySignedThenOkWithLaunchUrl()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions(), ct);
        using var client = host.CreateClient();
        var issuedAt = host.Clock.GetUtcNow();

        var launch = await OperatorApi.LaunchDemoAsync(client, host, host.PrimaryKey, ct);

        AssertLaunchUrl(launch.LaunchUrl);
        Assert.Equal(issuedAt.AddSeconds(60), launch.ExpiresAt);

        // Both active keys of the demo operator work (rotation without downtime, ADR-0014).
        var withSecondKey = await OperatorApi.LaunchDemoAsync(client, host, host.SecondaryKey, ct);
        AssertLaunchUrl(withSecondKey.LaunchUrl);
        Assert.NotEqual(launch.LaunchToken, withSecondKey.LaunchToken, StringComparer.Ordinal);

        using var json = JsonDocument.Parse(launch.Body);
        Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("expiresAt").ValueKind);
    }

    [Fact(DisplayName = "Dado o operador demo e Postgres/Redis reais, quando 200 lançamentos assinados são medidos após aquecimento, então p99 ≤ 100 ms na máquina de referência")]
    [Trait("Spec", "SPEC-0009:IT-01")]
    [Trait("Category", "Integration")]
    [Trait("Category", "Performance")]
    public async Task GivenDemoOperatorWhenTwoHundredLaunchesAreTimedThenP99IsWithinOneHundredMilliseconds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions(), ct);
        using var client = host.CreateClient();
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var samples = new List<double>(Samples);
        for (var i = 0; i < WarmUps + Samples; i++)
        {
            if (i > 0 && i % BatchPerWindow == 0)
            {
                host.Clock.Advance(TimeSpan.FromSeconds(61));
            }

            var elapsed = await TimedLaunchAsync(client, host, tokens, ct);
            if (i >= WarmUps)
            {
                samples.Add(elapsed.TotalMilliseconds);
            }
        }

        Assert.Equal(WarmUps + Samples, tokens.Count);
        var sorted = samples.Order().ToList();
        var p99 = sorted[(int)Math.Ceiling(0.99 * Samples) - 1];
        var median = sorted[Samples / 2];
        var all = string.Join(", ", samples.Select(s => s.ToString("F2", CultureInfo.InvariantCulture)));
        Assert.True(
            p99 <= Budget.Budget.TotalMilliseconds,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Launch p99 was {p99:F2} ms (median {median:F2} ms), above the budget of {Budget.Budget.TotalMilliseconds} ms from {Budget.Source} over {Samples} calls. Samples (ms): {all}"));
    }

    private static async Task<TimeSpan> TimedLaunchAsync(HttpClient client, SecurityHost host, HashSet<string> tokens, CancellationToken ct)
    {
        using var request = OperatorApi.SignedLaunch(LaunchBodies.Demo(LaunchBodies.NewPlayerId()), host.PrimaryKey, host.UnixNow());
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        stopwatch.Stop();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body);
        var launchUrl = new Uri(json.RootElement.GetProperty("launchUrl").GetString()!);
        Assert.True(tokens.Add(LaunchUrls.TokenOf(launchUrl)), "Every launch must issue a new launch token.");
        return stopwatch.Elapsed;
    }

    private static void AssertLaunchUrl(Uri launchUrl)
    {
        // launchUrl = {gameBaseUrl}/?lt={launchToken}
        Assert.Equal(SecurityHost.GameBaseUrl.GetLeftPart(UriPartial.Authority), launchUrl.GetLeftPart(UriPartial.Authority));
        Assert.Equal("/", launchUrl.AbsolutePath);
        var token = LaunchUrls.TokenOf(launchUrl);
        Assert.True(token.Length >= 43, $"A 32-byte launch token needs at least 43 URL-safe characters, got {token.Length}.");
        Assert.True(token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'), $"The launch token must be URL-safe: {token}");
    }
}
