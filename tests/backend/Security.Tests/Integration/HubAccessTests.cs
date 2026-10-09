using System.Net;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Security.Tests.Integration;

/// <summary>
/// IT-05: the hub policy of <c>/hubs/rounds</c> (SPEC-0009 §6). The rounds hub itself belongs to SPEC-0008, developed in
/// parallel, so the tests map <see cref="RoundsHubStandIn"/> at the same path with no protection of its own: the session
/// authentication, deny by default and the sala check must all come from what SPEC-0009 registers in the Host.
/// </summary>
[Collection(SecurityDependencies.Name)]
public sealed class HubAccessTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const string NegotiatePath = RoundsHubStandIn.Path + "/negotiate?negotiateVersion=1";

    [Fact(DisplayName = "Dado um cliente SignalR com sessão da sala demo, quando JoinSala(\"outra\"), então HubException FORBIDDEN; JoinSala(\"demo\") é aceito")]
    [Trait("Spec", "SPEC-0009:IT-05")]
    [Trait("Category", "Integration")]
    public async Task GivenDemoSessionWhenJoiningAnotherSalaThenForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var session = await NewSessionAsync(client, host, ct);

        await using var probe = await HubProbe.ConnectAsync(host.Server, RoundsHubStandIn.Path, session, ct);

        // Positive control: the session's own sala goes through to the hub method.
        var own = await probe.InvokeAsync("JoinSala", "demo", ct);
        Assert.True(own.Error is null, $"JoinSala(\"demo\") with a demo session must be accepted: {own.Raw}");
        Assert.Equal("\"demo|demo\"", own.Result);

        var other = await probe.InvokeAsync("JoinSala", "outra", ct);
        Assert.True(other.Error is not null && other.Error.Contains("FORBIDDEN", StringComparison.Ordinal), $"Expected HubException(FORBIDDEN), got: {other.Raw}");
        Assert.Null(other.Result);
    }

    [Fact(DisplayName = "Dado o hub /hubs/rounds, quando conectado sem token, então a conexão é recusada com 401")]
    [Trait("Spec", "SPEC-0009:IT-05")]
    [Trait("Category", "Integration")]
    public async Task GivenNoTokenWhenConnectingThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();

        using var response = await client.PostAsync(new Uri(NegotiatePath, UriKind.Relative), content: null, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "Dado o hub /hubs/rounds, quando o access_token é forjado (assinatura quebrada, claim sala trocada ou alg none), então 401; com a sessão válida, 200")]
    [Trait("Spec", "SPEC-0009:IT-05")]
    [Trait("Category", "Integration")]
    public async Task GivenForgedSessionTokenWhenConnectingThenUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(ct);
        using var client = host.CreateClient();
        var session = await NewSessionAsync(client, host, ct);

        using (var valid = await client.PostAsync(new Uri(NegotiatePath + "&access_token=" + Uri.EscapeDataString(session), UriKind.Relative), content: null, ct))
        {
            Assert.True(valid.StatusCode == HttpStatusCode.OK, $"The valid session must be accepted by the hub, got {(int)valid.StatusCode}.");
        }

        foreach (var (name, forged) in new[]
        {
            ("broken signature", JwtParts.WithBrokenSignature(session)),
            ("sala claim changed to 'outra'", JwtParts.WithClaim(session, "sala", "outra")),
            ("alg none", JwtParts.Unsigned(session)),
            ("random text", "not-a-session"),
        })
        {
            using var response = await client.PostAsync(new Uri(NegotiatePath + "&access_token=" + Uri.EscapeDataString(forged), UriKind.Relative), content: null, ct);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"A forged session ({name}) must be refused with 401, got {(int)response.StatusCode}.");
        }
    }

    private static async Task<string> NewSessionAsync(HttpClient client, SecurityHost host, CancellationToken ct)
    {
        var launch = await OperatorApi.LaunchDemoAsync(client, host, host.PrimaryKey, ct).ConfigureAwait(false);
        return await OperatorApi.CreateSessionTokenAsync(client, launch.LaunchToken, ct).ConfigureAwait(false);
    }

    private Task<SecurityHost> StartAsync(CancellationToken ct) =>
        SecurityHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, new SecurityHostOptions { WithRoundsHubStandIn = true }, ct);
}
