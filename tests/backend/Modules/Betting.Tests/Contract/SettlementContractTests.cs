using System.Text.Json;
using Apogeu.Betting.Tests.Support;
using Apogeu.Fairness.Contracts;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Wallet.Contracts;
using Xunit;

namespace Apogeu.Betting.Tests.Contract;

[Collection(BettingIntegration.Name)]
public sealed class SettlementContractTests(BettingContainers containers)
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);

    [Fact(DisplayName = "Dadas as mensagens reais BetSettled e BetRefunded (refunded e pending) de IT-01 e IT-03, quando validadas contra os tipos gerados do hub /hubs/bets (SPEC-0006), então conferem")]
    [Trait("Spec", "SPEC-0024:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenGeneratedHubContractWhenRealSettlementMessagesAreValidatedThenAllConform()
    {
        var ct = TestContext.Current.CancellationToken;
        var manifest = await BetsHubManifest.GenerateAsync(ct);
        var wallet = new ProgrammableWallet();
        var mismatch = new RevealMismatch();
        var (name1, name2) = ("player-" + Guid.NewGuid().ToString("N"), "player-" + Guid.NewGuid().ToString("N"));

        // The refunds of the second player are not confirmed (IT-03): its BetRefunded is pending.
        var down = BettingHost.PlayerRef(name2);
        wallet.OnRollback = op => op.Player == down ? new WalletUnavailable(Retryable: true) : null;
        await using var host = await BettingHost.StartAsync(containers, ct, wallet, services =>
        {
            FixedCrashFairness.Register(services, 2.00m);
            services.Decorate<IFairnessService>((inner, _) => new AnnullingFairness(inner, mismatch));
        });
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var p1 = await BetsClient.ConnectAsync(host, name1, ct);
        await using var p2 = await BetsClient.ConnectAsync(host, name2, ct);

        // IT-01: a bet lost in a crashed round, then bets refunded in an annulled round.
        var (crashedFlight, lost) = await CashOutFlight.PlaceAndFlyAsync(
            host,
            rounds,
            async () => [BetsClient.BetId(await p1.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false))],
            ct);
        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == crashedFlight.RoundId, TimeSpan.FromSeconds(60));
        mismatch.ArmNextReveal();
        var (annulledFlight, refunded) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        [
            BetsClient.BetId(await p1.PlaceAsync(panel: 1, amount: "20.50", ct).ConfigureAwait(false)),
            BetsClient.BetId(await p2.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
        ], ct);
        await rounds.Recorder.WaitForAsync("RoundAnnulled", e => e.RoundId == annulledFlight.RoundId, TimeSpan.FromSeconds(60));

        var settled = await p1.WaitForEventAsync("BetSettled", lost[0], EventTimeout);
        var confirmed = await p1.WaitForEventAsync("BetRefunded", refunded[0], EventTimeout);
        var pending = await p2.WaitForEventAsync("BetRefunded", refunded[1], EventTimeout);
        Assert.Equal("refunded", Json.Text(confirmed.Payload, "refundStatus"));
        Assert.Equal("pending", Json.Text(pending.Payload, "refundStatus"));

        var violations = new List<string>();
        foreach (var sent in (HubEvent[])[settled, confirmed, pending])
        {
            violations.AddRange(HubSchemaValidator.Validate(manifest.ClientArgument(sent.Target), sent.Payload)
                .Select(error => $"{sent.Target}: {error} in {sent.Payload.GetRawText()}"));
        }

        Assert.True(violations.Count == 0, "Messages that do not conform to the generated hub contract:" + Environment.NewLine + string.Join(Environment.NewLine, violations));

        // The generated types are strict: an unknown status or refund status, or a missing refund status, does not conform.
        var betId = lost[0];
        var roundId = crashedFlight.RoundId;
        Assert.NotEmpty(HubSchemaValidator.Validate(manifest.ClientArgument("BetSettled"), Synthetic($$"""{"betId":"{{betId}}","roundId":"{{roundId}}","status":"won"}""")));
        Assert.NotEmpty(HubSchemaValidator.Validate(manifest.ClientArgument("BetRefunded"), Synthetic($$"""{"betId":"{{betId}}","roundId":"{{roundId}}","refunded":"10.00","refundStatus":"later","balance":null}""")));
        Assert.NotEmpty(HubSchemaValidator.Validate(manifest.ClientArgument("BetRefunded"), Synthetic($$"""{"betId":"{{betId}}","roundId":"{{roundId}}","refunded":"10.00","balance":"3000.00"}""")));
    }

    private static JsonElement Synthetic(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
