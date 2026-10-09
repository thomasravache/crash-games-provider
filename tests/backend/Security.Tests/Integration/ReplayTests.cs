using System.Net;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Integration;

/// <summary>IT-03: a signed request seen before inside the window is a replay (SPEC-0009 §4), detected through Redis.</summary>
[Collection(SecurityDependencies.Name)]
public sealed class ReplayTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    [Fact(DisplayName = "Dada a mesma requisição assinada enviada duas vezes dentro da janela, então a primeira recebe 200 e a segunda 409 REPLAY_DETECTED")]
    [Trait("Spec", "SPEC-0009:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenSameSignedRequestTwiceWithinWindowThenSecondIsReplayDetected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions(), ct);
        using var client = host.CreateClient();
        var body = LaunchBodies.Demo(LaunchBodies.NewPlayerId());
        var timestamp = host.UnixNow();

        using (var first = OperatorApi.SignedLaunch(body, host.PrimaryKey, timestamp))
        using (var response = await client.SendAsync(first, ct))
        {
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"The first request must be accepted, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        }

        // Ten seconds later, still inside the ±300 s window: same bytes, same signature.
        host.Clock.Advance(TimeSpan.FromSeconds(10));
        using (var replay = OperatorApi.SignedLaunch(body, host.PrimaryKey, timestamp))
        using (var response = await client.SendAsync(replay, ct))
        {
            await ErrorBodies.AssertErrorAsync(response, HttpStatusCode.Conflict, "REPLAY_DETECTED", ct);
        }
    }
}
