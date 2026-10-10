using System.Globalization;
using System.Text.Json;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features;
using Apogeu.Betting.Features.CancelBet;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Features.Sessions;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.Betting.Tests.Support;
using Apogeu.BuildingBlocks.Contracts.Json;
using Apogeu.TestKit.Logging;
using Apogeu.TestKit.Time;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

/// <summary>
/// UT-07 on the cancellation use case (<see cref="CancelBetUseCase"/>) with in-memory doubles only: the bet store, the round
/// feed, the wallet (SPEC-0021@2 contract), Wolverine's <see cref="TestMessageContext"/> as the bus and a captured log. The
/// use case is built from that container, so a new dependency of it shows up as a missing registration, not a compile error.
/// </summary>
public sealed class CancelRefundTests
{
    private const string Sala = "demo";

    [Fact(DisplayName = "Dada uma aposta placed cancelada, quando o estorno não confirma, então BetCancelled com refundStatus pending e um segundo CancelBet devolve o mesmo resultado")]
    [Trait("Spec", "SPEC-0022:UT-07")]
    public async Task GivenPlacedBetWhenRefundIsNotConfirmedThenPendingAndSecondCancelReturnsSameResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var wallet = new ProgrammableWallet { OnRollback = _ => new WalletUnavailable(Retryable: true) };
        using var scenario = Scenario.Create(wallet);
        var bet = scenario.SeedPlaced(10.00m);

        var first = await scenario.CancelAsync(bet, ct);
        var second = await scenario.CancelAsync(bet, ct);

        var cancelled = Cancelled(first);
        Assert.Equal(bet.Id, cancelled.GetProperty("betId").GetGuid());
        Assert.True(cancelled.TryGetProperty("refundStatus", out var status), $"BetCancelled has refundStatus (contract v3): {cancelled.GetRawText()}");
        Assert.Equal("pending", status.GetString());
        Assert.Equal(10.00m, Money(cancelled, "refunded"));
        Assert.Equal(BetStatus.Cancelled, scenario.State.StatusOf(bet.Id));

        // Idempotent: the same BetCancelled again, and no second refund attempt is needed to answer it.
        Assert.Equal(cancelled.GetRawText(), Cancelled(second).GetRawText());
    }

    [Fact(DisplayName = "Dada uma aposta placed cancelada, quando a carteira responde TransactionNotFound ao estorno, então refunded 0,00 e alerta de conciliação")]
    [Trait("Spec", "SPEC-0022:UT-07")]
    public async Task GivenPlacedBetWhenWalletAnswersTransactionNotFoundThenZeroRefundedAndReconciliationAlert()
    {
        var ct = TestContext.Current.CancellationToken;

        // The wallet never saw the debit of this bet, so its rollback is TransactionNotFound (final, SPEC-0021@2).
        var wallet = new ProgrammableWallet();
        using var scenario = Scenario.Create(wallet);
        var bet = scenario.SeedPlaced(10.00m);

        var outcome = await scenario.CancelAsync(bet, ct);

        var cancelled = Cancelled(outcome);
        Assert.Equal(0.00m, Money(cancelled, "refunded"));
        Assert.True(cancelled.TryGetProperty("refundStatus", out var status), $"BetCancelled has refundStatus (contract v3): {cancelled.GetRawText()}");
        Assert.Equal("refunded", status.GetString());
        Assert.Equal(BetStatus.Cancelled, scenario.State.StatusOf(bet.Id));
        Assert.Contains(wallet.Inner.Calls, c => c.Result is WalletTransactionNotFound);

        // Reconciliation alert: a warning (or worse) that names the bet.
        var betId = bet.Id.ToString();
        Assert.True(
            scenario.Logs.Entries.Any(e => e.Level >= LogLevel.Warning && e.Message.Contains(betId, StringComparison.OrdinalIgnoreCase)),
            $"Expected a reconciliation alert (log ≥ Warning naming bet {betId}). Logs:{Environment.NewLine}{string.Join(Environment.NewLine, scenario.Logs.Entries.Select(e => $"{e.Level} {e.Category}: {e.Message}"))}");
    }

    private static JsonElement Cancelled(BettingOutcome<BetCancelled> outcome)
    {
        Assert.True(outcome.Value is not null, $"Expected a BetCancelled, got the error {outcome.Code}.");
        return JsonSerializer.SerializeToElement(outcome.Value, ContractJson.Options);
    }

    private static decimal Money(JsonElement element, string property) =>
        decimal.Parse(element.GetProperty(property).GetString()!, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    private sealed class Scenario : IDisposable
    {
        private readonly ServiceProvider _provider;

        private Scenario(ServiceProvider provider, InMemoryBetStore state, LogCapture logs, RoundWindow round)
        {
            _provider = provider;
            State = state;
            Logs = logs;
            Round = round;
        }

        public InMemoryBetStore State { get; }

        public LogCapture Logs { get; }

        public RoundWindow Round { get; }

        public static Scenario Create(IOperatorWallet wallet)
        {
            var clock = TestClock.Create();
            var round = Bets.Betting(number: 1, endsAt: clock.GetUtcNow().AddSeconds(5));
            var (store, state) = InMemoryBetStore.Create();
            var logs = new LogCapture();
            var services = new ServiceCollection();
            services.AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
            services.AddMetrics();
            services.AddSingleton<BettingMetrics>();
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton(store);
            services.AddSingleton<IRoundFeed>(new FixedRoundFeed(round));
            services.AddSingleton(wallet);
            var bus = new TestMessageContext();
            services.AddSingleton<IMessageBus>(bus);
            services.AddSingleton<IMessageContext>(bus);
            return new Scenario(services.BuildServiceProvider(), state, logs, round);
        }

        public Bet SeedPlaced(decimal amount)
        {
            var placement = Bet.Place(Guid.NewGuid(), Bets.Player, Bets.Request(amount: amount), Bets.BrlRules, Round, Round.BettingEndsAt.AddSeconds(-4), []);
            var bet = Bets.Accepted(placement);
            State.Seed(new AcceptedBet(bet, Sala, BetStatus.Placed, bet.RoundId, bet.RoundNumber, FakeOperatorWallet.OpeningBalance - amount));
            return bet;
        }

        public Task<BettingOutcome<BetCancelled>> CancelAsync(Bet bet, CancellationToken ct)
        {
            var useCase = ActivatorUtilities.CreateInstance<CancelBetUseCase>(_provider);
            return useCase.CancelAsync(new BettingSession(Bets.Player, Sala, BettingSession.DemoMode), new CancelBetRequest(bet.Id), ct);
        }

        public void Dispose()
        {
            _provider.Dispose();
            Logs.Dispose();
        }
    }
}
