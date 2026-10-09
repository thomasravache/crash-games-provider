using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Tests.Support;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FairnessMultiplier = Apogeu.Fairness.Contracts.Multiplier;

namespace Apogeu.Rounds.Tests.Integration;

[Collection(RoundsIntegration.Name)]
public sealed class VerificationTests(RoundsContainers containers)
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Dada uma rodada encerrada com Postgres real, quando GET /verification, então os dados passam no Verify da SPEC-0007; para rodada em voo, 409 ROUND_NOT_FINISHED e nada expõe crashPoint ou semente da rodada em voo")]
    [Trait("Spec", "SPEC-0008:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenCrashedRoundWhenVerificationIsRequestedThenItPassesVerifyAndInFlightRoundExposesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await RoundsCluster.CreateAsync(containers, sala: null, ct);
        var host = cluster.StartHost();
        using var http = host.CreateClient();
        await using var connection = host.CreateHubConnection();
        var recorder = new HubRecorder(connection);
        await connection.StartAsync(ct);
        await connection.InvokeAsync<JsonElement>("JoinSala", SalaSettings.SalaId, ct);

        var unknown = await http.GetAsync(new Uri($"/api/v1/rounds/{Guid.NewGuid()}/verification", UriKind.Relative), ct);
        await AssertProblemAsync(unknown, HttpStatusCode.NotFound, "ROUND_NOT_FOUND", ct);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var from = recorder.Events.Count;
            var betting = await recorder.WaitForAsync("RoundBetting", RoundTimeout, from);
            var flying = await recorder.WaitForAsync("RoundFlying", e => e.RoundId == betting.RoundId, RoundTimeout, from);
            var roundId = flying.RoundId;

            // While the round flies: the verification, the snapshot and the history.
            var inFlight = await http.GetAsync(new Uri($"/api/v1/rounds/{roundId}/verification", UriKind.Relative), ct);
            var inFlightBody = await inFlight.Content.ReadAsStringAsync(ct);
            var snapshot = await connection.InvokeAsync<JsonElement>("JoinSala", SalaSettings.SalaId, ct);
            var history = await http.GetStringAsync(new Uri("/api/v1/salas/demo/rounds?limit=100", UriKind.Relative), ct);
            var crashed = await recorder.WaitForAsync("RoundCrashed", e => e.RoundId == roundId, RoundTimeout, from);
            if (inFlight.StatusCode == HttpStatusCode.OK || !IsFlying(snapshot, roundId))
            {
                // The round crashed (≈ 1.00x) before the requests: not a round in flight, take the next one.
                continue;
            }

            var seed = Json.Text(crashed.Payload, "revealedSeed");
            var crashPoint = Json.Text(crashed.Payload, "crashPoint");
            await AssertProblemAsync(inFlight, (HttpStatusCode)409, "ROUND_NOT_FINISHED", inFlightBody);
            var current = snapshot.GetProperty("current");
            Assert.True(
                Json.IsNullOrMissing(current, "crashPoint") && Json.IsNullOrMissing(current, "revealedSeed"),
                $"The snapshot of a round in flight must not carry its crash point or seed: {snapshot.GetRawText()}");
            Assert.DoesNotContain(roundId.ToString(), history, StringComparison.OrdinalIgnoreCase);
            var beforeCrash = recorder.Events.TakeWhile(e => !ReferenceEquals(e, crashed)).Where(e => e.RoundId == roundId).ToList();
            foreach (var exposed in beforeCrash.Select(e => e.Payload.GetRawText()).Concat([snapshot.GetRawText(), history, inFlightBody]))
            {
                Assert.True(
                    !exposed.Contains(seed, StringComparison.OrdinalIgnoreCase),
                    $"The seed of round {roundId} was exposed before its crash: {exposed}");
            }

            Assert.True(
                beforeCrash.All(e => Json.IsNullOrMissing(e.Payload, "crashPoint")),
                $"A hub message carried the crash point before the crash: {string.Join(Environment.NewLine, beforeCrash)}");

            await AssertVerificationAsync(host, http, betting, roundId, seed, crashPoint, cluster.Sala, ct);
            return;
        }

        Assert.Fail($"No round was caught in flight in 8 rounds. Received:{Environment.NewLine}{recorder.Describe()}");
    }

    // From the G4 review of SPEC-0008: the verification of a crashed round uses the parameters it was played with, not the
    // room's current configuration; a room removed from the configuration keeps its rounds verifiable.
    [Fact(DisplayName = "Dada uma rodada encerrada, quando a configuração da sala muda (Rtp/Cap/GrowthK) ou a sala sai da configuração e o host reinicia, então GET /verification devolve o rtp/cap originais, passa no Verify da SPEC-0007 e não vira ROUND_NOT_FOUND")]
    [Trait("Spec", "SPEC-0008:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenCrashedRoundWhenSalaConfigurationChangesThenVerificationKeepsOriginalParameters()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cluster = await RoundsCluster.CreateAsync(containers, sala: null, ct);
        var original = cluster.StartHost();
        HubEvent betting;
        HubEvent crashed;
        await using (var connection = original.CreateHubConnection())
        {
            var recorder = new HubRecorder(connection);
            await connection.StartAsync(ct);
            await connection.InvokeAsync<JsonElement>("JoinSala", SalaSettings.SalaId, ct);
            betting = await recorder.WaitForAsync("RoundBetting", RoundTimeout);
            crashed = await recorder.WaitForAsync("RoundCrashed", e => e.RoundId == betting.RoundId, RoundTimeout);
        }

        await original.DisposeAsync();
        var roundId = crashed.RoundId;
        var seed = Json.Text(crashed.Payload, "revealedSeed");
        var crashPoint = Json.Text(crashed.Payload, "crashPoint");

        // Same room, new parameters: every one differs from the round's (RTP 0.97, cap 2.00, k 0.00006).
        var reconfigured = cluster.StartHost(new RoundsHostSetup
        {
            Salas = new Dictionary<string, SalaSettings>(StringComparer.Ordinal)
            {
                [SalaSettings.SalaId] = new(Rtp: "0.92", Cap: "5.00", GrowthK: "0.0001"),
            },
        });
        using (var http = reconfigured.CreateClient())
        {
            await AssertVerificationAsync(reconfigured, http, betting, roundId, seed, crashPoint, cluster.Sala, ct);
        }

        await reconfigured.DisposeAsync();

        // The room leaves the configuration (another room is configured, so the default demo room is not seeded).
        var withoutSala = cluster.StartHost(new RoundsHostSetup
        {
            Salas = new Dictionary<string, SalaSettings>(StringComparer.Ordinal) { ["outra"] = new() },
        });
        using (var http = withoutSala.CreateClient())
        {
            await AssertVerificationAsync(withoutSala, http, betting, roundId, seed, crashPoint, cluster.Sala, ct);
        }
    }

    private static async Task AssertVerificationAsync(
        RoundsHost host,
        HttpClient http,
        HubEvent betting,
        Guid roundId,
        string revealedSeed,
        string crashPoint,
        SalaSettings sala,
        CancellationToken ct)
    {
        var response = await http.GetAsync(new Uri($"/api/v1/rounds/{roundId}/verification", UriKind.Relative), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 for a crashed round, got {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement;

        Assert.Equal(revealedSeed, Json.Text(data, "seed"));
        Assert.Equal(Json.Text(betting.Payload, "commitment"), Json.Text(data, "previousSeed"));
        Assert.Equal(crashPoint, Json.Text(data, "crashPoint"));
        Assert.Equal(sala.Rtp, Json.Text(data, "rtp"));
        Assert.Equal(sala.Cap, Json.Text(data, "cap"));

        var seed = Seed.FromHex(Json.Text(data, "seed"));
        var result = VerifyWithFairness(host, seed, data);
        Assert.True(result == VerificationResult.Valid, $"SPEC-0007 Verify returned {result} for {body}");

        // s_r = h_{N-r}: hashing the seed at most N times reaches the published terminal hash.
        var terminal = Convert.FromHexString(Json.Text(data, "terminalHash"));
        var link = seed.ToArray();
        var reached = false;
        for (var i = 0; i < sala.ChainLength && !reached; i++)
        {
            link = SHA256.HashData(link);
            reached = link.AsSpan().SequenceEqual(terminal);
        }

        Assert.True(reached, $"The seed does not chain to the terminal hash within {sala.ChainLength} links: {body}");
    }

    private static VerificationResult VerifyWithFairness(RoundsHost host, Seed seed, JsonElement data)
    {
        using var scope = host.Services.CreateScope();
        var fairness = scope.ServiceProvider.GetRequiredService<IFairnessService>();
        return fairness.Verify(
            seed,
            Seed.FromHex(Json.Text(data, "previousSeed")),
            Salt.FromHex(Json.Text(data, "salt")),
            new Rtp(Json.Decimal(data, "rtp")),
            new FairnessMultiplier(Json.Decimal(data, "cap")),
            new CrashPoint(Json.Decimal(data, "crashPoint")));
    }

    private static bool IsFlying(JsonElement snapshot, Guid roundId) =>
        snapshot.TryGetProperty("current", out var current)
        && current.ValueKind == JsonValueKind.Object
        && current.GetProperty("roundId").GetGuid() == roundId
        && string.Equals(Json.Text(current, "state"), "flying", StringComparison.Ordinal);

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code, CancellationToken ct) =>
        await AssertProblemAsync(response, status, code, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)).ConfigureAwait(false);

    private static Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code, string body)
    {
        Assert.True(response.StatusCode == status, $"Expected {(int)status} {code}, got {(int)response.StatusCode}: {body}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(body);
        Assert.True(
            problem.RootElement.TryGetProperty("code", out var actual) && string.Equals(actual.GetString(), code, StringComparison.Ordinal),
            $"Expected the ProblemDetails extension code = {code}: {body}");
        return Task.CompletedTask;
    }
}
