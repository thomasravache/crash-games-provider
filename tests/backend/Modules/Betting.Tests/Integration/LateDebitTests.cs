using System.Diagnostics;
using Apogeu.Betting.Features;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Tests.Support;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Apogeu.Betting.Tests.Integration;

[Collection(BettingIntegration.Name)]
public sealed class LateDebitTests(BettingContainers containers)
{
    private static readonly string[] AcceptedStatuses = ["placed", "active", "queued"];

    [Fact(DisplayName = "Dado um débito que a carteira só aplica depois de a varredura estornar a reserva (TransactionNotFound), quando o débito tardio confirma, então a aposta não é aceita, um novo estorno durável é aplicado uma única vez (saldo final igual ao inicial) e um alerta de conciliação é registrado")]
    [Trait("Spec", "SPEC-0022:IT-12")]
    [Trait("Category", "Integration")]
    public async Task GivenDebitAppliedAfterSweeperReversedWhenItConfirmsThenLateDebitIsReversedOnceWithReconciliationAlert()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        await using var host = await BettingHost.StartAsync(containers, ct, wallet);
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        var player = "player-" + Guid.NewGuid().ToString("N");
        var account = BettingHost.PlayerRef(player);
        await using var bets = await BetsClient.ConnectAsync(host, player, ct);
        var logsAtLateDebit = -1;

        // The operator applies the debit only after the sweeper voided the reservation and its rollback found no debit.
        wallet.BeforeDebit = async op =>
        {
            if (op.Player != account)
            {
                return;
            }

            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(60) && !SweptWithoutDebit(wallet, op.BetId))
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            Interlocked.CompareExchange(ref logsAtLateDebit, host.Logs.Entries.Count, -1);
        };

        await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
        var outcome = await bets.PlaceAsync(panel: 1, amount: "10.00", ct).WaitAsync(TimeSpan.FromSeconds(90), ct);

        // PlaceBet answers before PendingTimeout; the held debit is applied only after the sweeper's rollback, later.
        await Eventually.HoldsAsync(
            () => wallet.Inner.AppliedDebits.Count == 1,
            TimeSpan.FromSeconds(60),
            () => $"the held debit applied after the sweeper's rollback (calls: {string.Join(", ", wallet.Inner.Calls)})");
        var debit = Assert.Single(wallet.Inner.AppliedDebits);
        Assert.True(SweptWithoutDebit(wallet, debit.BetId), $"Precondition: the sweeper's rollback ran before the debit (calls: {string.Join(", ", wallet.Inner.Calls)}).");
        Assert.True(outcome.Result is null, $"The bet whose reservation was voided is not accepted; got {outcome}");

        // The late debit is reversed by a new durable reversal, once.
        await Eventually.HoldsAsync(
            () => wallet.Inner.AppliedRollbacks.Any(r => r.BetId == debit.BetId),
            TimeSpan.FromSeconds(30),
            () => $"the late debit of bet {debit.BetId} reversed (calls: {string.Join(", ", wallet.Inner.Calls)})");
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        var reversal = Assert.Single(wallet.Inner.AppliedRollbacks, r => r.BetId == debit.BetId);
        Assert.Equal(debit.TransactionId, reversal.OriginalTransactionId);
        Assert.Equal(FakeOperatorWallet.OpeningBalance, wallet.Inner.BalanceOf(account, BettingHost.Brl));
        var status = await host.BetStatusAsync(debit.BetId, ct);
        Assert.True(status is null || !AcceptedStatuses.Contains(status, StringComparer.Ordinal), $"The bet is not accepted (status {status}).");

        // Reconciliation alert about the late debit: logged after it was applied, at Warning or above, naming the bet.
        var betId = debit.BetId.ToString();
        var after = host.Logs.Entries.Skip(Math.Max(0, logsAtLateDebit)).ToList();
        Assert.True(
            after.Any(e => e.Level >= LogLevel.Warning && e.Message.Contains(betId, StringComparison.OrdinalIgnoreCase)),
            $"Expected a reconciliation alert (log ≥ Warning naming bet {betId}) after the late debit. Logs since:{Environment.NewLine}{string.Join(Environment.NewLine, after.Select(e => $"{e.Level} {e.Category}: {e.Message}"))}");
    }

    [Fact(DisplayName = "Dada uma carteira que não responde ao débito, quando o jogador aposta, então cada tentativa de débito tem tempo-limite e a resposta (WALLET_UNAVAILABLE) chega antes do tempo da varredura")]
    [Trait("Spec", "SPEC-0022:IT-12")]
    [Trait("Category", "Integration")]
    public async Task GivenWalletNotAnsweringDebitWhenPlayerBetsThenAttemptsTimeOutBeforePendingTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet();
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        wallet.BeforeDebit = _ => never.Task;
        await using var host = await BettingHost.StartAsync(containers, ct, wallet);
        var pendingTimeout = host.Services.GetRequiredService<IOptions<BettingOptions>>().Value.PendingTimeout;
        await using var rounds = await RoundWatch.StartAsync(host, ct);
        await using var bets = await BetsClient.ConnectAsync(host, "player-" + Guid.NewGuid().ToString("N"), ct);

        try
        {
            await rounds.NextBettingAsync(TimeSpan.FromSeconds(1.5));
            var watch = Stopwatch.StartNew();
            HubOutcome? outcome = null;
            try
            {
                outcome = await bets.PlaceAsync(panel: 1, amount: "10.00", ct).WaitAsync(pendingTimeout + TimeSpan.FromSeconds(5), ct);
            }
            catch (TimeoutException)
            {
                // Asserted below.
            }

            Assert.True(
                outcome is not null && watch.Elapsed < pendingTimeout,
                $"PlaceBet must answer before Betting:PendingTimeout ({pendingTimeout}) when the wallet does not answer: each debit attempt has a timeout. Elapsed {watch.Elapsed}, answer {outcome?.ToString() ?? "none"}.");
            Assert.True(outcome!.FailedWith(Apogeu.Betting.Contracts.BettingErrorCodes.WalletUnavailable), $"Expected WALLET_UNAVAILABLE, got {outcome}");
        }
        finally
        {
            never.TrySetResult();
        }
    }

    [Fact(DisplayName = "Dado Betting:PendingTimeout menor que o total das tentativas de débito, quando o Host inicia, então a validação das opções recusa a inicialização")]
    [Trait("Spec", "SPEC-0022:IT-12")]
    [Trait("Category", "Integration")]
    public async Task GivenPendingTimeoutBelowDebitAttemptsWhenHostStartsThenOptionsValidationFails()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new Dictionary<string, string>(StringComparer.Ordinal) { ["Betting:PendingTimeout"] = "00:00:00.001" };

        BettingHost? started = null;
        Exception? failure = null;
        try
        {
            started = await BettingHost.StartAsync(containers, ct, settings: settings);
        }
#pragma warning disable CA1031 // Any startup failure is inspected below.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            failure = exception;
        }
        finally
        {
            if (started is not null)
            {
                await started.DisposeAsync();
            }
        }

        var validation = Chain(failure).OfType<OptionsValidationException>().FirstOrDefault();
        Assert.True(
            validation is not null && validation.Failures.Any(f => f.Contains("PendingTimeout", StringComparison.Ordinal)),
            $"Expected the startup to fail with an OptionsValidationException about PendingTimeout; got {failure?.ToString() ?? "a host that started"}.");
    }

    private static bool SweptWithoutDebit(ProgrammableWallet wallet, Guid betId) =>
        wallet.Inner.Calls.Any(c => string.Equals(c.Kind, "rollback", StringComparison.Ordinal) && c.BetId == betId && c.Result is WalletTransactionNotFound);

    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Chain))
                {
                    yield return inner;
                }
            }
        }
    }
}
