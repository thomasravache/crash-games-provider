using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class RoundClosedTests(BettingContainers containers)
{
    private static readonly string[] AcceptedStatuses = ["placed", "active", "queued"];

    [Fact(DisplayName = "Dado o crash da rodada entre o débito e a confirmação de uma aposta placed, quando a aposta é confirmada, então ROUND_CLOSED e o débito é estornado")]
    [Trait("Spec", "SPEC-0022:IT-10")]
    [Trait("Category", "Integration")]
    public async Task GivenRoundCrashedBetweenDebitAndCommitWhenBetIsConfirmedThenRoundClosedAndDebitReversed()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        await using var host = await BettingHost.StartAsync(containers, ct, wallet);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(player);
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);

        var betting = await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
        var crashed = rounds.Recorder.WaitForAsync("RoundCrashed", e => e.RoundId == betting.RoundId, TimeSpan.FromSeconds(60));

        // The wallet answers the debit only after the round of the bet crashed (and Betting had time to see it).
        wallet.AfterDebit = async op =>
        {
            if (op.Player == account)
            {
                await crashed.ConfigureAwait(false);
                await Task.Delay(500).ConfigureAwait(false);
            }
        };

        var outcome = await bets.PlaceAsync(panel: 1, amount: "10.00", ct);

        // ROUND_CLOSED is new in the contract v3 (emenda 3): literal, so the test compiles before the constant exists.
        Assert.True(outcome.FailedWith("ROUND_CLOSED"), $"Expected ROUND_CLOSED, got {outcome}");
        var debit = Assert.Single(wallet.Inner.AppliedDebits);
        await Eventually.HoldsAsync(
            () => wallet.Inner.AppliedRollbacks.Any(r => r.BetId == debit.BetId),
            TimeSpan.FromSeconds(30),
            () => $"the debit of bet {debit.BetId} reversed after ROUND_CLOSED (calls: {string.Join(", ", wallet.Inner.Calls)})");
        Assert.Single(wallet.Inner.AppliedRollbacks, r => r.BetId == debit.BetId);
        Assert.Equal(FakeOperatorWallet.OpeningBalance, wallet.Inner.BalanceOf(account, BettingHost.Brl));
        var status = await host.BetStatusAsync(debit.BetId, ct);
        Assert.True(status is null || !AcceptedStatuses.Contains(status, StringComparer.Ordinal), $"A bet refused with ROUND_CLOSED is not accepted (status {status}).");
    }
}
