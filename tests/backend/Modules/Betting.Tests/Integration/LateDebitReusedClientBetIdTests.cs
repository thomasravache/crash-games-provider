using System.Diagnostics;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class LateDebitReusedClientBetIdTests(BettingContainers containers)
{
    private static readonly string[] AcceptedStatuses = ["placed", "active", "queued"];

    [Fact(DisplayName = "Dada uma aposta X cujo débito fica retido até depois da varredura, quando o cliente refaz a aposta com o mesmo clientBetId (B2 aceita) e só então o débito de X é aplicado, então o débito de X é estornado uma única vez, um alerta de conciliação cita X e B2 continua aceita com o próprio débito intacto")]
    [Trait("Spec", "SPEC-0022:IT-13")]
    [Trait("Category", "Integration")]
    public async Task GivenLateDebitOfVoidedBetWhenClientBetIdWasReusedThenLateDebitIsReversedAndNewBetIsUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        await using var host = await BettingHost.StartAsync(containers, ct, wallet);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(player);
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);
        var clientBetId = Guid.NewGuid();
        var hold = new FirstDebitHold(account, wallet, host);

        // Only the first bet's debit (X) is held: until the sweeper voided X (rollback → TransactionNotFound) and the
        // re-placed bet B2 was accepted. Then the operator applies it.
        wallet.BeforeDebit = hold.HoldAsync;

        try
        {
            await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
            var refused = await bets.PlaceAsync(clientBetId, panel: 1, amount: "10.00", autoCashOut: null, ct).WaitAsync(TimeSpan.FromSeconds(90), ct);
            Assert.True(refused.Result is null, $"X is answered as not accepted while its debit is held; got {refused}");

            await Eventually.HoldsAsync(
                () => hold.FirstBet != Guid.Empty && SweptWithoutDebit(wallet, hold.FirstBet),
                TimeSpan.FromSeconds(60),
                () => $"the sweeper voided X and its rollback found no debit (calls: {string.Join(", ", wallet.Inner.Calls)})");

            // The client re-places with the same clientBetId: a new bet B2, accepted with its own debit.
            var replaced = (await bets.PlaceAsync(clientBetId, panel: 1, amount: "10.00", autoCashOut: null, ct)).Value;
            var secondBet = replaced.GetProperty("betId").GetGuid();
            Assert.NotEqual(hold.FirstBet, secondBet);
            Assert.Contains(Json.Text(replaced, "status"), (IEnumerable<string>)AcceptedStatuses, StringComparer.Ordinal);
            hold.Release();

            // X's debit lands late: it is reversed once, with a reconciliation alert naming X.
            await Eventually.HoldsAsync(
                () => wallet.Inner.AppliedDebits.Any(d => d.BetId == hold.FirstBet),
                TimeSpan.FromSeconds(60),
                () => $"X's held debit applied (calls: {string.Join(", ", wallet.Inner.Calls)})");
            var lateDebit = wallet.Inner.AppliedDebits.Single(d => d.BetId == hold.FirstBet);
            await Eventually.HoldsAsync(
                () => wallet.Inner.AppliedRollbacks.Any(r => r.OriginalTransactionId == lateDebit.TransactionId),
                TimeSpan.FromSeconds(30),
                () => $"X's late debit reversed (calls: {string.Join(", ", wallet.Inner.Calls)})");
            await Task.Delay(TimeSpan.FromSeconds(3), ct);

            await AssertSettledAsync(host, wallet, account, lateDebit, secondBet, hold.LogsAtLateDebit, ct);
        }
        finally
        {
            hold.Release();
        }
    }

    /// <summary>X's late debit reversed once with an alert naming X; B2 accepted with its debit intact; balance = start − B2.</summary>
    private static async Task AssertSettledAsync(
        BettingHost host,
        ProgrammableWallet wallet,
        PlayerRef account,
        WalletCall lateDebit,
        Guid secondBet,
        int logsAtLateDebit,
        CancellationToken ct)
    {
        var firstBet = lateDebit.BetId;
        Assert.Single(wallet.Inner.AppliedRollbacks, r => r.OriginalTransactionId == lateDebit.TransactionId);
        var xId = firstBet.ToString();
        var after = host.Logs.Entries.Skip(Math.Max(0, logsAtLateDebit)).ToList();
        Assert.True(
            after.Any(e => e.Level >= LogLevel.Warning && e.Message.Contains(xId, StringComparison.OrdinalIgnoreCase)),
            $"Expected a reconciliation alert (log ≥ Warning naming X {xId}) after its late debit. Logs since:{Environment.NewLine}{string.Join(Environment.NewLine, after.Select(e => $"{e.Level} {e.Category}: {e.Message}"))}");

        // B2 is untouched: accepted, its debit applied and never reversed.
        var secondDebit = Assert.Single(wallet.Inner.AppliedDebits, d => d.BetId == secondBet);
        Assert.DoesNotContain(wallet.Inner.AppliedRollbacks, r => r.OriginalTransactionId == secondDebit.TransactionId || r.BetId == secondBet);
        var status = await host.BetStatusAsync(secondBet, ct).ConfigureAwait(false);
        Assert.True(status is not null && AcceptedStatuses.Contains(status, StringComparer.Ordinal), $"B2 stays accepted (status {status}).");
        Assert.Equal(FakeOperatorWallet.OpeningBalance - 10.00m, wallet.Inner.BalanceOf(account, BettingHost.Brl));
    }

    private static bool SweptWithoutDebit(ProgrammableWallet wallet, Guid betId) =>
        wallet.Inner.Calls.Any(c => string.Equals(c.Kind, "rollback", StringComparison.Ordinal) && c.BetId == betId && c.Result is WalletTransactionNotFound);

    /// <summary>
    /// Holds only the first debit of the player (bet X) until the sweeper voided X (its rollback found no debit) and the
    /// re-placed bet B2 was accepted; then the operator applies it. Other debits (B2's) go through.
    /// </summary>
    private sealed class FirstDebitHold(PlayerRef account, ProgrammableWallet wallet, BettingHost host)
    {
        private readonly TaskCompletionSource _secondAccepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock _gate = new();
        private Guid _firstBet;
        private int _logsAtLateDebit = -1;

        public Guid FirstBet
        {
            get
            {
                lock (_gate)
                {
                    return _firstBet;
                }
            }
        }

        /// <summary>How many log entries the host had when X's debit was released (the alert comes after).</summary>
        public int LogsAtLateDebit => Volatile.Read(ref _logsAtLateDebit);

        public void Release() => _secondAccepted.TrySetResult();

        public async Task HoldAsync(WalletOperation op)
        {
            if (op.Player != account)
            {
                return;
            }

            lock (_gate)
            {
                if (_firstBet == Guid.Empty)
                {
                    _firstBet = op.BetId;
                }
                else if (op.BetId != _firstBet)
                {
                    return;
                }
            }

            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(60) && !SweptWithoutDebit(wallet, op.BetId))
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            await _secondAccepted.Task.WaitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            Interlocked.CompareExchange(ref _logsAtLateDebit, host.Logs.Entries.Count, -1);
        }
    }
}
