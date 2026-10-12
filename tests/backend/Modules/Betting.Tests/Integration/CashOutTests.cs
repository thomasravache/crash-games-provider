using System.Globalization;
using System.Text.Json;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Wallet.Contracts;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

/// <summary>
/// Cash-out on the real Host (SPEC-0023): the real conductor of the room <c>demo</c> (k = 0,0002/ms) with the crash point of
/// every round fixed by <see cref="FixedCrashFairness"/>, real PostgreSQL and Redis, a real SignalR player and the wallet
/// double of the SPEC-0021 contract.
/// </summary>
[Collection(BettingIntegration.Name)]
public sealed class CashOutTests(BettingContainers containers)
{
    private static readonly TimeSpan RoundTimeout = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Dado o Host real com uma rodada conduzida, quando o jogador retira no meio do voo e outro pedido chega depois do crash, então o primeiro recebe CashedOut com saldo creditado e o segundo CASHOUT_TOO_LATE")]
    [Trait("Spec", "SPEC-0023:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenConductedRoundWhenPlayerCashesOutMidFlightAndAfterCrashThenCashedOutAndTooLate()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        await using var host = await StartAsync(wallet, crashPoint: 2.00m, ct);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(player);
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);

        var (flight, placed) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        [
            BetsClient.BetId(await bets.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await bets.PlaceAsync(panel: 2, amount: "5.00", ct).ConfigureAwait(false)),
        ], ct);
        var (early, late) = (placed[0], placed[1]);

        // Mid-flight: 1 s of a 3,47 s flight.
        await flight.AtAsync(TimeSpan.FromSeconds(1), ct);
        var sentAt = DateTimeOffset.UtcNow;
        var outcome = await bets.CashOutAsync(early, ct);
        var answeredAt = DateTimeOffset.UtcNow;

        var cashedOut = CashOutFlight.CashedOutOf(outcome);
        var multiplier = Json.Decimal(cashedOut, "multiplier");
        Assert.Equal(early, cashedOut.GetProperty("betId").GetGuid());
        Assert.False(cashedOut.GetProperty("auto").GetBoolean());
        flight.AssertReceivedBetween(multiplier, sentAt, answeredAt, outcome.ToString());
        var payout = CashOutMath.Payout(10.00m, multiplier);
        Assert.Equal(payout, Json.Decimal(cashedOut, "payout"));
        Assert.Equal("credited", Json.Text(cashedOut, "creditStatus"));
        var balance = FakeOperatorWallet.OpeningBalance - 15.00m + payout;
        Assert.Equal(balance, Json.Decimal(cashedOut, "balance"));

        // The credit: once, with the deterministic key of the bet, for the payout.
        var credit = Assert.Single(wallet.Inner.AppliedCredits);
        Assert.Equal(TransactionId.ForCredit(early), credit.TransactionId);
        Assert.Equal(early, credit.BetId);
        Assert.Equal(payout, credit.Amount);
        Assert.Equal(balance, wallet.Inner.BalanceOf(account, BettingHost.Brl));

        // After the crash the other bet can no longer be cashed out, and nothing is credited for it.
        await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, RoundTimeout);
        var tooLate = await bets.CashOutAsync(late, ct);
        Assert.True(tooLate.FailedWith("CASHOUT_TOO_LATE"), $"Expected CASHOUT_TOO_LATE after the crash, got {tooLate}");
        Assert.DoesNotContain(wallet.Inner.Calls, c => string.Equals(c.Kind, "credit", StringComparison.Ordinal) && c.BetId == late);
        Assert.Equal(balance, wallet.Inner.BalanceOf(account, BettingHost.Brl));
    }

    [Fact(DisplayName = "Dadas duas apostas com cash-out automático (1,50x abaixo e 3,00x acima do crash 2,00x), quando a rodada termina, então só a primeira recebe CashedOut auto no alvo exato")]
    [Trait("Spec", "SPEC-0023:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenAutoCashOutsBelowAndAboveCrashWhenRoundEndsThenOnlyTheFirstIsCashedOutAtItsTarget()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        await using var host = await StartAsync(wallet, crashPoint: 2.00m, ct, new BettingSala(Cap: "3.00"));
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);

        var (flight, placed) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        [
            BetsClient.BetId(await bets.PlaceAsync(Guid.NewGuid(), panel: 1, amount: "10.00", autoCashOut: "1.50", ct).ConfigureAwait(false)),
            BetsClient.BetId(await bets.PlaceAsync(Guid.NewGuid(), panel: 2, amount: "10.00", autoCashOut: "3.00", ct).ConfigureAwait(false)),
        ], ct);
        var (below, above) = (placed[0], placed[1]);
        var crashed = await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, RoundTimeout);
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        var events = bets.DescribeEvents();
        var auto = Assert.Single(bets.EventsOf("CashedOut", below)).Payload;
        Assert.True(auto.GetProperty("auto").GetBoolean(), $"The automatic cash-out is auto:true. {events}");
        Assert.Equal("1.50", Json.Text(auto, "multiplier"));
        Assert.Equal(15.00m, Json.Decimal(auto, "payout"));
        Assert.Equal("credited", Json.Text(auto, "creditStatus"));
        Assert.Equal(FakeOperatorWallet.OpeningBalance - 20.00m + 15.00m, Json.Decimal(auto, "balance"));

        // Executed at t_alvo = ln(1,5)/k ≈ 2,03 s, never before it. The notice comes from the credit handler (SPEC-0023 §6),
        // so on a slow machine it may arrive after the crash; the exact 1,50x above proves where it was executed.
        var executedNoEarlierThan = flight.StartedAt + CashOutMath.TimeTo(flight.K, 1.50m);
        var received = bets.EventsOf("CashedOut", below)[0].ReceivedAt;
        Assert.True(received >= executedNoEarlierThan - TimeSpan.FromMilliseconds(1), $"CashedOut auto received at {received:O}, before t_alvo {executedNoEarlierThan:O}.");

        Assert.Empty(bets.EventsOf("CashedOut", above));
        var credit = Assert.Single(wallet.Inner.AppliedCredits);
        Assert.Equal(TransactionId.ForCredit(below), credit.TransactionId);
        Assert.Equal(15.00m, credit.Amount);
        Assert.DoesNotContain(wallet.Inner.Calls, c => string.Equals(c.Kind, "credit", StringComparison.Ordinal) && c.BetId == above);
    }

    [Fact(DisplayName = "Dada uma carteira que falha no crédito, quando o jogador retira, o processo reinicia e o CashOut é repetido, então a primeira resposta tem creditStatus pending e balance nulo, existe um único crédito confirmado e BalanceUpdated chega ao jogador")]
    [Trait("Spec", "SPEC-0023:IT-03")]
    [Trait("Category", "Integration")]
    public async Task GivenFailingCreditWhenCashedOutRestartedAndRepeatedThenOneCreditAndBalanceUpdated()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        var player = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(player);
        var creditsDown = 1;
        wallet.OnCredit = op => op.Player == account && Volatile.Read(ref creditsDown) == 1 ? new WalletUnavailable(Retryable: true) : null;
        var victim = await StartAsync(wallet, crashPoint: 2.00m, ct);
        try
        {
            await using var survivor = await BettingHost.StartPeerAsync(victim, services => FixedCrashFairness.Register(services, 2.00m));
            await Eventually.HoldsAsync(() => victim.IsLeading || survivor.IsLeading, TimeSpan.FromSeconds(30), () => "one instance leading the room");
            var leader = victim.IsLeading ? victim : survivor;

            // The phases as Betting receives them on the leader (its outbox delivers the round events there).
            await using (var rounds = await RoundWatch.StartAsync(leader, ct))
            {
                await using var onVictim = await BetsClient.ConnectAsync(victim, player, ct);
                var (flight, placed) = await CashOutFlight.PlaceAndFlyAsync(
                    victim,
                    rounds,
                    async () => [BetsClient.BetId(await onVictim.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false))],
                    ct);
                var betId = placed[0];
                await flight.AtAsync(TimeSpan.FromSeconds(1), ct);
                var first = await onVictim.CashOutAsync(betId, ct);

                var pending = CashOutFlight.CashedOutOf(first);
                Assert.Equal("pending", Json.Text(pending, "creditStatus"));
                Assert.True(Json.IsNullOrMissing(pending, "balance"), $"No balance while the credit is not confirmed: {first}");
                Assert.True(wallet.CreditAttempts >= 1, $"Precondition: the credit was attempted and failed (calls: {string.Join(", ", wallet.Inner.Calls)}).");
                Assert.Empty(wallet.Inner.AppliedCredits);

                // The process that cashed out stops for good.
                await victim.KillAsync(TimeSpan.FromSeconds(10));

                // The same player on the surviving instance repeats the cash-out; the wallet comes back afterwards.
                await RepeatAndRecoverAsync(survivor, wallet, player, betId, pending, () => Volatile.Write(ref creditsDown, 0), ct);
            }
        }
        finally
        {
            await victim.DisposeAsync();
        }
    }

    [Fact(DisplayName = "Dadas todas as respostas e eventos enviados antes do crash, quando inspecionados, então nenhum contém o ponto nem o instante do crash")]
    [Trait("Spec", "SPEC-0023:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenEveryAnswerAndEventBeforeCrashWhenInspectedThenNoneRevealsCrashPointOrInstant()
    {
        const decimal crashPoint = 1.87m;
        var ct = TestContext.Current.CancellationToken;
        await using var host = await StartAsync(new ProgrammableWallet(), crashPoint, ct);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var bets = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct);

        // Every kind of answer before the crash: bets, an automatic cash-out, a manual one, its repetition and refusals.
        var (flight, placed) = await CashOutFlight.PlaceAndFlyAsync(host, rounds, async () =>
        [
            BetsClient.BetId(await bets.PlaceAsync(panel: 1, amount: "10.00", ct).ConfigureAwait(false)),
            BetsClient.BetId(await bets.PlaceAsync(Guid.NewGuid(), panel: 2, amount: "10.00", autoCashOut: "1.50", ct).ConfigureAwait(false)),
        ], ct);
        await bets.LimitsAsync(ct);
        await flight.AtAsync(TimeSpan.FromSeconds(1), ct);
        _ = CashOutFlight.CashedOutOf(await bets.CashOutAsync(placed[0], ct));
        await bets.CashOutAsync(placed[0], ct);
        await bets.CashOutAsync(Guid.NewGuid(), ct);
        await bets.WaitForEventAsync("CashedOut", placed[1], TimeSpan.FromSeconds(5));
        await bets.CashOutAsync(placed[1], ct);
        var crashed = await rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == flight.RoundId, RoundTimeout);

        var crashedAt = Json.Instant(crashed.Payload, "crashedAt");
        var crashTime = CashOutMath.TimeTo(flight.K, crashPoint);
        var violations = new List<string>();
        foreach (var outcome in bets.Outcomes)
        {
            if (outcome.Result is { } result)
            {
                Inspect(result, outcome.ToString(), crashPoint, crashedAt, crashTime, violations);
            }
            else if (outcome.Error is { } error && (error.Contains("1.87", StringComparison.Ordinal) || error.Contains("crash", StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add($"{outcome}: the error reveals the crash");
            }
        }

        foreach (var sent in bets.Events.Where(e => e.ReceivedAt < crashed.ReceivedAt))
        {
            Inspect(sent.Payload, sent.ToString(), crashPoint, crashedAt, crashTime, violations);
        }

        foreach (var sent in rounds.Recorder.Events.Where(e => e.ReceivedAt < crashed.ReceivedAt))
        {
            Inspect(sent.Payload, sent.ToString(), crashPoint, crashedAt, crashTime, violations);
        }

        Assert.True(bets.Outcomes.Count(o => string.Equals(o.Method, "CashOut", StringComparison.Ordinal)) == 4, "Precondition: four CashOut answers inspected.");
        Assert.NotEmpty(bets.Events);
        Assert.True(violations.Count == 0, "Answers or events before the crash reveal it:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// IT-03 after the restart: the repeated cash-out answers the same CashedOut; once the wallet is back (<paramref name="recover"/>)
    /// the credit is retried until confirmed, once, and the player is told the new balance.
    /// </summary>
    private static async Task RepeatAndRecoverAsync(BettingHost survivor, ProgrammableWallet wallet, string player, Guid betId, JsonElement pending, Action recover, CancellationToken ct)
    {
        var account = BettingHost.PlayerRef(player);
        var onSurvivor = await BetsClient.ConnectAsync(survivor, player, ct).ConfigureAwait(false);
        await using (onSurvivor.ConfigureAwait(false))
        {
            var repeated = await onSurvivor.CashOutAsync(betId, ct).ConfigureAwait(false);
            var again = CashOutFlight.CashedOutOf(repeated);
            Assert.Equal(betId, again.GetProperty("betId").GetGuid());
            Assert.Equal(Json.Text(pending, "multiplier"), Json.Text(again, "multiplier"));
            Assert.Equal(Json.Text(pending, "payout"), Json.Text(again, "payout"));
            Assert.False(again.GetProperty("auto").GetBoolean());

            recover();
            var payout = Json.Decimal(pending, "payout");
            var updated = await onSurvivor.WaitForEventAsync("BalanceUpdated", betId: null, TimeSpan.FromSeconds(90)).ConfigureAwait(false);
            var balance = FakeOperatorWallet.OpeningBalance - 10.00m + payout;
            Assert.Equal(balance, Json.Decimal(updated.Payload, "balance"));
            Assert.Equal("BRL", Json.Text(updated.Payload, "currency"));

            await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
            var credit = Assert.Single(wallet.Inner.AppliedCredits);
            Assert.Equal(TransactionId.ForCredit(betId), credit.TransactionId);
            Assert.Equal(payout, credit.Amount);
            Assert.Equal(balance, wallet.Inner.BalanceOf(account, BettingHost.Brl));
        }
    }

    /// <summary>
    /// Fails <paramref name="violations"/> with any member of <paramref name="element"/> that names the crash, equals the crash
    /// point, is the crash instant, or is t_c in milliseconds or seconds.
    /// </summary>
    private static void Inspect(JsonElement element, string where, decimal crashPoint, DateTimeOffset crashedAt, TimeSpan crashTime, List<string> violations)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Contains("crash", StringComparison.OrdinalIgnoreCase))
                    {
                        violations.Add($"{where}: member '{property.Name}'");
                    }

                    Inspect(property.Value, where, crashPoint, crashedAt, crashTime, violations);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Inspect(item, where, crashPoint, crashedAt, crashTime, violations);
                }

                break;
            case JsonValueKind.String:
                var text = element.GetString()!;
                if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    InspectNumber(number, where, crashPoint, crashTime, violations);
                }
                else if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant)
                    && (instant - crashedAt).Duration() < TimeSpan.FromMilliseconds(2))
                {
                    violations.Add($"{where}: the crash instant {text}");
                }

                break;
            case JsonValueKind.Number:
                InspectNumber(element.GetDecimal(), where, crashPoint, crashTime, violations);
                break;
        }
    }

    private static void InspectNumber(decimal number, string where, decimal crashPoint, TimeSpan crashTime, List<string> violations)
    {
        var milliseconds = (decimal)crashTime.TotalMilliseconds;
        if (number == crashPoint
            || Math.Abs(number - milliseconds) < 1m
            || Math.Abs(number - (milliseconds / 1000m)) < 0.001m)
        {
            violations.Add(string.Create(CultureInfo.InvariantCulture, $"{where}: the value {number} is the crash point or t_c"));
        }
    }

    private Task<BettingHost> StartAsync(ProgrammableWallet wallet, decimal crashPoint, CancellationToken ct, BettingSala? sala = null) =>
        BettingHost.StartAsync(containers, ct, wallet, services => FixedCrashFairness.Register(services, crashPoint), sala);
}
