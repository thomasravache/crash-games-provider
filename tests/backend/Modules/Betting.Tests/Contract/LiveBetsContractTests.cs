using System.Globalization;
using System.Net;
using System.Text.Json;
using Apogeu.Betting.Tests.Support;
using Apogeu.Contracts.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Security.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Contract;

/// <summary>
/// CT-01 (SPEC-0025 §6, ADR-0008): the real messages of the live bets (IT-01) validate against the generated hub contract
/// (<c>JoinSalaBets</c> → <c>LiveBetsSnapshot</c> and the client method <c>LiveBetsBatch</c>), and the real responses of
/// <c>GET /api/v1/me/bets</c> (IT-02) against the OpenAPI document the Host generates — the sources of the TypeScript
/// contract consumed by SPEC-0026.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class LiveBetsContractTests(BettingContainers containers)
{
    private const string OpenApiPath = "/openapi/v1.json";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "Dado o contrato gerado do hub /hubs/bets, quando o snapshot de JoinSalaBets e os LiveBetsBatch reais são validados, então conferem")]
    [Trait("Spec", "SPEC-0025:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedHubContractWhenRealSnapshotAndBatchesAreValidatedThenTheyConform()
    {
        var ct = TestContext.Current.CancellationToken;
        var manifest = await BetsHubManifest.GenerateAsync(ct);
        await using var host = await BettingHost.StartAsync(
            containers,
            ct,
            services: services =>
            {
                FixedCrashFairness.Register(services, 2.00m);
                RivalOperatorLimits.Register(services);
            },
            sala: new BettingSala(BettingDuration: "00:00:06"));
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var marcos = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct, nickname: "Marcos");
        await using var other = await BetsClient.ConnectAsync(
            host, "player-" + Guid.NewGuid().ToString("N"), ct, operatorId: RivalOperatorLimits.Operator);

        // The messages of IT-01: a bet with a nickname, one without, a cash-out, and the snapshots before and after it.
        var (flight, bets) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        {
            var first = await marcos.JoinSalaBetsAsync(ct).ConfigureAwait(false);
            Assert.True(first.Error is null, $"JoinSalaBets: {first}");
            return
            [
                BetsClient.BetId(await marcos.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
                BetsClient.BetId(await other.PlaceAsync(panel: 1, amount: "20.50", ct).ConfigureAwait(false)),
            ];
        }, ct);
        var placedBatch = await marcos.WaitForLiveBatchAsync(b => b.GetProperty("totalBets").GetInt32() == 2, Timeout);
        await flight.AtAsync(TimeSpan.FromSeconds(1), ct);
        _ = CashOutFlight.CashedOutOf(await marcos.CashOutAsync(bets[0], ct));
        var cashOutBatch = await marcos.WaitForLiveBatchAsync(b => b.TryGetProperty("cashedOut", out var c) && c.GetArrayLength() > 0, Timeout);
        var snapshot = (await other.JoinSalaBetsAsync(ct)).Value;

        var violations = new List<string>();
        foreach (var outcome in marcos.Outcomes.Concat(other.Outcomes).Where(o => string.Equals(o.Method, "JoinSalaBets", StringComparison.Ordinal)))
        {
            violations.AddRange(HubSchemaValidator.Validate(manifest.ServerResult("JoinSalaBets"), outcome.Value)
                .Select(error => $"JoinSalaBets: {error} in {outcome.Value.GetRawText()}"));
        }

        foreach (var batch in marcos.LiveBatches.Concat(other.LiveBatches))
        {
            violations.AddRange(HubSchemaValidator.Validate(manifest.ClientArgument(BetsClient.LiveBetsBatchTarget), batch.Payload)
                .Select(error => $"LiveBetsBatch: {error} in {batch.Payload.GetRawText()}"));
        }

        Assert.True(violations.Count == 0, "Messages that do not conform to the generated hub contract:" + Environment.NewLine + string.Join(Environment.NewLine, violations));

        // The real messages exercised the optional members: a bet without a nickname, and a cash-out on the snapshot.
        Assert.Contains(placedBatch.Payload.GetProperty("placed").EnumerateArray(), b => string.Equals(Json.Text(b, "nickname"), "Jogador***", StringComparison.Ordinal));
        Assert.Single(cashOutBatch.Payload.GetProperty("cashedOut").EnumerateArray());
        Assert.Contains(snapshot.GetProperty("bets").EnumerateArray(), b => !Json.IsNullOrMissing(b, "multiplier") && !Json.IsNullOrMissing(b, "payout"));
        Assert.Contains(snapshot.GetProperty("bets").EnumerateArray(), b => Json.IsNullOrMissing(b, "multiplier"));

        // Money and multipliers are decimal strings (ADR-0008).
        Assert.Equal(JsonValueKind.String, snapshot.GetProperty("totalWagered").ValueKind);
        Assert.Equal(30.50m, Json.Decimal(snapshot, "totalWagered"));
    }

    [Fact(DisplayName = "Dado o OpenAPI gerado, quando as respostas reais de GET /api/v1/me/bets (200, 400 INVALID_LIMIT, 401) são validadas, então conferem com o contrato")]
    [Trait("Spec", "SPEC-0025:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedOpenApiWhenRealMyBetsResponsesAreValidatedThenTheyConform()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await BettingHost.StartAsync(containers, ct, services: services => FixedCrashFairness.Register(services, 2.00m));
        using var anonymous = host.CreateHttpClient();
        var openApi = await OpenApiAsync(anonymous, ct);

        // The schema of the contract: statuses on the wire and the optional members.
        var ok = openApi.Resolve(SchemaOf(openApi, "200", "application/json"));
        var item = openApi.Resolve(openApi.Resolve(openApi.Property(ok, "items")).GetProperty("items"));
        var statuses = openApi.Resolve(openApi.Property(item, "status")).GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal);
        Assert.True(
            statuses.SetEquals(["placed", "queued", "active", "cashed_out", "lost", "cancelled", "refunded"]),
            $"The statuses of the history are those of SPEC-0025 §6, got [{string.Join(", ", statuses)}].");
        foreach (var optional in (string[])["roundId", "roundNumber", "autoCashOut", "multiplier", "payout"])
        {
            Assert.True(openApi.IsNullable(openApi.Property(item, optional)), $"{optional} of a history item is optional (SPEC-0025 §6).");
        }

        Assert.True(openApi.IsNullable(openApi.Property(ok, "nextCursor")), "nextCursor is optional (SPEC-0025 §6).");

        // Real responses: a cashed-out bet and a queued one without a round.
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var me = "player-" + Guid.NewGuid().ToString("N");
        await using var player = await BetsClient.ConnectAsync(host, me, ct, nickname: "Marcos");
        var (flight, bets) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
            [BetsClient.BetId(await player.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false))], ct);
        await flight.AtAsync(TimeSpan.FromSeconds(0.5), ct);
        _ = CashOutFlight.CashedOutOf(await player.CashOutAsync(bets[0], ct));
        var queued = BetsClient.BetId(await player.PlaceAsync(panel: 2, amount: "5.00", ct));
        using var client = host.CreatePlayerHttpClient(me, "Marcos");

        var (_, okText) = await AssertConformsAsync(openApi, client, MyBetsApi.PageUri(50), HttpStatusCode.OK, ct);
        using (var page = JsonDocument.Parse(okText))
        {
            var items = page.RootElement.GetProperty("items").EnumerateArray().ToList();
            Assert.Contains(items, i => i.GetProperty("betId").GetGuid() == queued && Json.IsNullOrMissing(i, "roundId"));
            Assert.Contains(items, i => i.GetProperty("betId").GetGuid() == bets[0] && string.Equals(Json.Text(i, "status"), "cashed_out", StringComparison.Ordinal));
        }

        var (badSchema, badText) = await AssertConformsAsync(openApi, client, MyBetsApi.PageUri(0), HttpStatusCode.BadRequest, ct);
        Assert.Contains("string", openApi.TypeNames(openApi.Property(badSchema, "code")));
        Assert.Equal("INVALID_LIMIT", ErrorBodies.CodeOf(badText));

        // 401 without a session; its body, when there is one, is the contract's error envelope.
        using var unauthorized = await anonymous.GetAsync(MyBetsApi.PageUri(10), ct);
        var unauthorizedText = await unauthorized.Content.ReadAsStringAsync(ct);
        Assert.True(unauthorized.StatusCode == HttpStatusCode.Unauthorized, $"Expected 401, got {(int)unauthorized.StatusCode}: {unauthorizedText}");
        if (!string.IsNullOrWhiteSpace(unauthorizedText))
        {
            using var body = JsonDocument.Parse(unauthorizedText);
            var schema = SchemaOf(openApi, "401", unauthorized.Content.Headers.ContentType?.MediaType ?? "application/json");
            Assert.Empty(OpenApiSchemaValidator.Validate(openApi, schema, body.RootElement));
        }

        _ = (await player.CancelAsync(queued, ct)).Value;
    }

    private static async Task<OpenApiDocument> OpenApiAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(new Uri(OpenApiPath, UriKind.Relative), ct).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return OpenApiDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    /// <summary>Sends the request, then validates the body against the schema of that status and media type.</summary>
    private static async Task<(JsonElement Schema, string Body)> AssertConformsAsync(
        OpenApiDocument openApi, HttpClient client, Uri uri, HttpStatusCode status, CancellationToken ct)
    {
        using var response = await client.GetAsync(uri, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Assert.True(response.StatusCode == status, $"Expected {(int)status} from GET {uri}, got {(int)response.StatusCode}: {text}");
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        Assert.True(mediaType is "application/json" or "application/problem+json", $"Expected a JSON body for {(int)status}, got '{mediaType}'.");

        var schema = SchemaOf(openApi, ((int)status).ToString(CultureInfo.InvariantCulture), mediaType!);
        using var body = JsonDocument.Parse(text);
        var errors = OpenApiSchemaValidator.Validate(openApi, schema, body.RootElement);
        Assert.True(
            errors.Count == 0,
            $"GET {uri} {(int)status} does not validate against the OpenAPI schema:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}{Environment.NewLine}Body: {text}");
        return (schema, text);
    }

    private static JsonElement SchemaOf(OpenApiDocument openApi, string status, string mediaType)
    {
        if (openApi.Root.TryGetProperty("paths", out var paths)
            && paths.TryGetProperty(MyBetsApi.Path, out var path)
            && path.TryGetProperty("get", out var operation)
            && operation.TryGetProperty("responses", out var responses)
            && responses.TryGetProperty(status, out var response)
            && response.TryGetProperty("content", out var content)
            && content.TryGetProperty(mediaType, out var media)
            && media.TryGetProperty("schema", out var schema))
        {
            return schema;
        }

        Assert.Fail($"The OpenAPI document has no '{status}' {mediaType} response schema for GET {MyBetsApi.Path}.");
        return default;
    }
}
