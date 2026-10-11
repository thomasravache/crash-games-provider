using System.Text.Json;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Contract;

[Collection(BettingIntegration.Name)]
public sealed class CashOutContractTests(BettingContainers containers)
{
    [Fact(DisplayName = "Dado o contrato v2 do cash-out no hub /hubs/bets gerado (SPEC-0006), quando as mensagens reais de IT-01/IT-02 são validadas contra o esquema gerado, então todas conferem")]
    [Trait("Spec", "SPEC-0023:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedCashOutContractWhenRealMessagesAreValidatedThenAllConform()
    {
        var ct = TestContext.Current.CancellationToken;
        var manifest = await BetsHubManifest.GenerateAsync(ct);
        await using var host = await BettingHost.StartAsync(
            containers,
            ct,
            new ProgrammableWallet(),
            services => FixedCrashFairness.Register(services, 2.00m),
            new BettingSala(Cap: "3.00"));
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var bets = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct);

        // The messages of IT-01 and IT-02: a manual cash-out, its repetition and an automatic one.
        var (flight, placed) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        [
            BetsClient.BetId(await bets.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await bets.PlaceAsync(Guid.NewGuid(), panel: 2, amount: "20.50", autoCashOut: "1.50", ct).ConfigureAwait(false)),
        ], ct);
        await flight.AtAsync(TimeSpan.FromMilliseconds(500), ct);
        var manual = await bets.CashOutAsync(placed[0], ct);
        await bets.CashOutAsync(placed[0], ct);
        await bets.WaitForEventAsync("CashedOut", placed[1], TimeSpan.FromSeconds(10));
        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, TimeSpan.FromSeconds(60));

        var violations = new List<string>();
        var cashOuts = bets.Outcomes.Where(o => string.Equals(o.Method, "CashOut", StringComparison.Ordinal)).ToList();
        foreach (var outcome in cashOuts)
        {
            violations.AddRange(HubSchemaValidator.Validate(manifest.ServerResult("CashOut"), outcome.Value)
                .Select(error => $"CashOut: {error} in {outcome.Value.GetRawText()}"));
        }

        foreach (var sent in bets.Events)
        {
            violations.AddRange(HubSchemaValidator.Validate(manifest.ClientArgument(sent.Target), sent.Payload)
                .Select(error => $"{sent.Target}: {error} in {sent.Payload.GetRawText()}"));
        }

        Assert.Equal(2, cashOuts.Count);
        _ = CashOutFlight.CashedOutOf(manual);
        Assert.Contains(bets.Events, e => string.Equals(e.Target, "CashedOut", StringComparison.Ordinal) && e.Payload.GetProperty("auto").GetBoolean());
        Assert.True(violations.Count == 0, "Messages that do not conform to the generated hub contract:" + Environment.NewLine + string.Join(Environment.NewLine, violations));

        AssertSyntheticShapes(manifest, placed[0]);
        AssertErrorCodes();
    }

    /// <summary>The shapes this run does not produce: a pending result, a pending credit, the late refusal and the balance update.</summary>
    private static void AssertSyntheticShapes(BetsHubManifest manifest, Guid betId)
    {
        var result = manifest.ServerResult("CashOut");
        Assert.Empty(HubSchemaValidator.Validate(result, Synthetic($$$"""{"cashedOut":null,"pending":{"betId":"{{{betId}}}"}}""")));
        Assert.Empty(HubSchemaValidator.Validate(result, Synthetic($$"""{"cashedOut":{"betId":"{{betId}}","multiplier":"1.23","payout":"12.30","creditStatus":"pending","balance":null,"auto":false},"pending":null}""")));
        Assert.NotEmpty(HubSchemaValidator.Validate(result, Synthetic($$"""{"cashedOut":{"betId":"{{betId}}","multiplier":"1.23","payout":"12.30","creditStatus":"later","balance":null,"auto":false},"pending":null}""")));
        Assert.NotEmpty(HubSchemaValidator.Validate(result, Synthetic($$"""{"cashedOut":{"betId":"{{betId}}","multiplier":"1.23","payout":"12.30","balance":"3002.30","auto":false},"pending":null}""")));
        Assert.Empty(HubSchemaValidator.Validate(manifest.ClientArgument("CashOutRejected"), Synthetic($$"""{"betId":"{{betId}}","code":"CASHOUT_TOO_LATE"}""")));
        Assert.Empty(HubSchemaValidator.Validate(manifest.ClientArgument("BalanceUpdated"), Synthetic("""{"balance":"3002.30","currency":"BRL"}""")));
    }

    /// <summary>The error codes of the contract v2 are published by the module.</summary>
    private static void AssertErrorCodes()
    {
        var codes = typeof(Apogeu.Betting.Contracts.BettingErrorCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("CASHOUT_TOO_LATE", codes);
        Assert.Contains("BET_NOT_ACTIVE", codes);
        Assert.Contains("BET_NOT_FOUND", codes);
    }

    private static JsonElement Synthetic(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
