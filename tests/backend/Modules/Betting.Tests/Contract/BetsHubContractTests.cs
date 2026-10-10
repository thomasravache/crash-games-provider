using System.Text.Json;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Contract;

[Collection(BettingIntegration.Name)]
public sealed class BetsHubContractTests(BettingContainers containers)
{
    private static readonly TimeSpan ActivationTimeout = TimeSpan.FromSeconds(30);

    [Fact(DisplayName = "Dado o contrato do hub /hubs/bets gerado (SPEC-0006), quando as mensagens reais de IT-01/IT-02 são validadas contra o esquema gerado, então todas conferem")]
    [Trait("Spec", "SPEC-0022:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedBetsHubContractWhenRealMessagesAreValidatedThenAllConform()
    {
        var ct = TestContext.Current.CancellationToken;
        var manifest = await BetsHubManifest.GenerateAsync(ct);
        await using var host = await BettingHost.StartAsync(containers, ct);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var bets = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct);

        // The messages of IT-01 and IT-02: limits, a queued bet and its activation, a placed bet with auto cash-out and its cancellation.
        var limits = (await bets.LimitsAsync(ct)).Value;
        await rounds.NextFlightAsync();
        var queued = BetsClient.BetId(await bets.PlaceAsync(panel: 1, amount: "10.00", ct));
        await bets.WaitForActivationAsync(queued, ActivationTimeout);
        var placed = BetsClient.BetId(await bets.PlaceAsync(Guid.NewGuid(), panel: 2, amount: "20.50", autoCashOut: "1.50", ct));
        _ = (await bets.CancelAsync(placed, ct)).Value;

        var violations = new List<string>();
        foreach (var outcome in bets.Outcomes)
        {
            violations.AddRange(HubSchemaValidator.Validate(manifest.ServerResult(outcome.Method), outcome.Value)
                .Select(error => $"{outcome.Method}: {error} in {outcome.Value.GetRawText()}"));
        }

        foreach (var activated in bets.Activated)
        {
            violations.AddRange(HubSchemaValidator.Validate(manifest.ClientArgument("BetActivated"), activated)
                .Select(error => $"BetActivated: {error} in {activated.GetRawText()}"));
        }

        var methods = bets.Outcomes.Select(o => o.Method).ToHashSet(StringComparer.Ordinal);
        Assert.True(methods.IsSupersetOf(["GetBetLimits", "PlaceBet", "CancelBet"]), $"Expected real results of every server method, got [{string.Join(", ", methods)}].");
        Assert.NotEmpty(bets.Activated);
        Assert.Contains(bets.Outcomes, o => string.Equals(o.Method, "PlaceBet", StringComparison.Ordinal) && string.Equals(Json.Text(o.Value, "status"), "queued", StringComparison.Ordinal));
        Assert.Contains(bets.Outcomes, o => string.Equals(o.Method, "PlaceBet", StringComparison.Ordinal) && string.Equals(Json.Text(o.Value, "status"), "placed", StringComparison.Ordinal));
        Assert.True(violations.Count == 0, "Messages that do not conform to the generated hub contract:" + Environment.NewLine + string.Join(Environment.NewLine, violations));

        // The limits of the demo operator in BRL (SPEC-0020@1 seed), as decimal strings (ADR-0008).
        Assert.Equal("BRL", Json.Text(limits, "currency"));
        Assert.Equal(JsonValueKind.String, limits.GetProperty("minBet").ValueKind);
        Assert.Equal(1.00m, Json.Decimal(limits, "minBet"));
        Assert.Equal(1000.00m, Json.Decimal(limits, "maxBet"));
        Assert.Equal(100000.00m, Json.Decimal(limits, "maxWin"));
    }
}
