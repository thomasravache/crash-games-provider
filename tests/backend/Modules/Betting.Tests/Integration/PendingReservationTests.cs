using System.Diagnostics;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Wallet.Contracts;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class PendingReservationTests(BettingContainers containers)
{
    private static readonly TimeSpan SweepBudget = TimeSpan.FromSeconds(30);
    // Every status a persisted (accepted) bet can reach, including those of settlement (SPEC-0024).
    private static readonly string[] AcceptedStatuses = ["placed", "active", "queued", "cashed_out", "lost", "refunded"];

    [Fact(DisplayName = "Dado um débito confirmado pela carteira e a instância interrompida antes do commit da aposta, quando a varredura de reservas pendentes roda, então o débito é estornado uma única vez em ≤ 30 s e a aposta não fica aceita")]
    [Trait("Spec", "SPEC-0022:IT-08")]
    [Trait("Category", "Integration")]
    public async Task GivenDebitedBetWhenInstanceDiesBeforeCommitThenSweeperReversesOnceWithinThirtySeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        var debited = new TaskCompletionSource<WalletOperation>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stuck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var victim = await BettingHost.StartAsync(containers, ct, wallet);
        await using var survivor = await BettingHost.StartPeerAsync(victim);
        var player = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(player);

        // The instance stops for good right after the wallet applied the debit, before Betting commits the bet.
        wallet.AfterDebit = op => op.Player == account ? Stop(op) : Task.CompletedTask;
        try
        {
            var bets = await BetsClient.ConnectAsync(victim, player, ct);
            var placing = bets.PlaceAsync(panel: 1, amount: "10.00", ct);
            _ = placing.ContinueWith(t => t.Exception, TaskScheduler.Default);

            var debit = await debited.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            var watch = Stopwatch.StartNew();
            await victim.KillAsync(TimeSpan.FromSeconds(10));

            var swept = await Eventually.HoldsAsync(
                () => Reversals(wallet, debit.BetId).Count >= 1,
                SweepBudget,
                () => $"the pending reservation of bet {debit.BetId} reversed by the sweeper (calls: {string.Join(", ", wallet.Inner.Calls)})");
            Assert.True(watch.Elapsed <= SweepBudget, $"Reversed after {watch.Elapsed}, budget {SweepBudget}.");

            // Once only, and the bet was never accepted.
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            var reversal = Assert.Single(Reversals(wallet, debit.BetId));
            Assert.Equal($"bet:{debit.BetId}:debit", reversal.OriginalTransactionId?.Value);
            Assert.Equal(FakeOperatorWallet.OpeningBalance, wallet.Inner.BalanceOf(account, BettingHost.Brl));
            var status = await survivor.BetStatusAsync(debit.BetId, ct);
            Assert.True(status is null || !AcceptedStatuses.Contains(status, StringComparer.Ordinal), $"The bet whose commit never happened is not accepted (status {status}).");
            _ = swept;
        }
        finally
        {
            // Lets the stuck invocation of the killed instance end (after the assertions).
            stuck.TrySetException(new OperationCanceledException("The instance was killed by IT-08."));
            await victim.DisposeAsync();
        }

        async Task Stop(WalletOperation op)
        {
            debited.TrySetResult(op);
            await stuck.Task.ConfigureAwait(false);
        }
    }

    [Fact(DisplayName = "Dada uma aposta efetivamente gravada com a resposta do commit perdida (cliente desconectado durante o commit), então ela não é estornada; se não ficou gravada, é estornada uma vez")]
    [Trait("Spec", "SPEC-0022:IT-08")]
    [Trait("Category", "Integration")]
    public async Task GivenAmbiguousCommitWhenBetWasPersistedThenItIsNotReversed()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        CommitFault? fault = null;
        await using var host = await BettingHost.StartAsync(containers, ct, wallet, services: services => fault = CommitFault.AddTo(services));
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(player);
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);

        // After the debit, the next write of Betting reaches PostgreSQL but its answer is lost.
        wallet.AfterDebit = op =>
        {
            if (op.Player == account)
            {
                fault!.ArmNextWrite();
            }

            return Task.CompletedTask;
        };

        await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
        var outcome = await bets.PlaceAsync(panel: 1, amount: "10.00", ct);
        var debit = Assert.Single(wallet.Inner.AppliedDebits);
        Assert.True(fault!.Injected == 1, $"The write after the debit was reported as lost ({fault.Injected} faults injected; answer {outcome}).");

        // Long enough for the sweeper (≤ 30 s) to act on a reservation it believes pending.
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(32) && Reversals(wallet, debit.BetId).Count == 0)
        {
            await Task.Delay(200, ct);
        }

        var status = await host.BetStatusAsync(debit.BetId, ct);
        var reversals = Reversals(wallet, debit.BetId);
        if (status is not null && AcceptedStatuses.Contains(status, StringComparer.Ordinal))
        {
            Assert.True(reversals.Count == 0, $"The bet was persisted ({status}) and must not be reversed, but {reversals.Count} reversal(s) were applied (answer {outcome}).");
            Assert.Equal(FakeOperatorWallet.OpeningBalance - 10.00m, wallet.Inner.BalanceOf(account, BettingHost.Brl));
        }
        else
        {
            Assert.True(reversals.Count == 1, $"The bet was not kept (status {status}), so its debit is reversed once; got {reversals.Count}.");
        }
    }

    private static List<WalletCall> Reversals(ProgrammableWallet wallet, Guid betId) =>
        [.. wallet.Inner.AppliedRollbacks.Where(r => r.BetId == betId)];
}
