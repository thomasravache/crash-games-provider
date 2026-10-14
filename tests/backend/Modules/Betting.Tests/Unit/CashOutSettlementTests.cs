using Apogeu.Betting.Domain;
using Apogeu.Betting.Features;
using Apogeu.Betting.Features.CashOut;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.LiveBets;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.Betting.Tests.Support;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.TestKit.Time;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

/// <summary>
/// The precedence of the automatic cash-out (SPEC-0023 §6, review G4 M3) on <see cref="CashOutSettlement"/> with in-memory
/// doubles only: a manual cash-out decided at or above the bet's target is settled at the exact target, as the automatic one.
/// </summary>
public sealed class CashOutSettlementTests
{
    private const string Sala = "demo";

    [Fact(DisplayName = "Dada uma aposta com alvo 1,50x, quando o cash-out manual é decidido com 1,62x (ou 1,50x), então é liquidada em 1,50x exatos com auto:true; com 1,49x, então em 1,49x com auto:false")]
    [Trait("Spec", "SPEC-0023:UT-05")]
    public async Task GivenTargetOnePointFiveWhenManualIsDecidedAboveThenSettledAtTargetAsAuto()
    {
        using var scenario = Scenario.Create();

        var above = scenario.SeedActive(autoCashOut: 1.50m, panel: 1);
        var atAbove = await scenario.SettleAsync(above, 1.62m);
        AssertCashedOut(atAbove, multiplier: 1.50m, payout: 15.00m, auto: true);
        Assert.Equal(new CashOutRecord(1.50m, 15.00m, Auto: true, Credited: true, Balance: scenario.BalanceAfter(above)), scenario.State.CashOutOf(above.Id));

        var atTarget = scenario.SeedActive(autoCashOut: 1.50m, panel: 2);
        AssertCashedOut(await scenario.SettleAsync(atTarget, 1.50m), multiplier: 1.50m, payout: 15.00m, auto: true);

        var below = scenario.SeedActive(autoCashOut: 1.50m, panel: 1);
        AssertCashedOut(await scenario.SettleAsync(below, 1.49m), multiplier: 1.49m, payout: 14.90m, auto: false);
        Assert.False(scenario.State.CashOutOf(below.Id)!.Auto);

        // The credits follow the settled payouts.
        Assert.Equal([15.00m, 15.00m, 14.90m], scenario.Wallet.AppliedCredits.Select(c => c.Amount!.Value));
    }

    private static void AssertCashedOut(CashedOut? cashedOut, decimal multiplier, decimal payout, bool auto)
    {
        Assert.NotNull(cashedOut);
        Assert.True(
            cashedOut.Multiplier.Value == multiplier && cashedOut.Payout.Value == payout && cashedOut.Auto == auto,
            $"Expected {multiplier}x, payout {payout}, auto:{auto}; got {cashedOut}");
        Assert.Equal(CreditStatus.Credited, cashedOut.CreditStatus);
    }

    private sealed class Scenario : IDisposable
    {
        private readonly ServiceProvider _provider;

        private Scenario(ServiceProvider provider, InMemoryBetStore state, FakeOperatorWallet wallet)
        {
            _provider = provider;
            State = state;
            Wallet = wallet;
        }

        public InMemoryBetStore State { get; }

        public FakeOperatorWallet Wallet { get; }

        public static Scenario Create()
        {
            var (store, state) = InMemoryBetStore.Create();
            var wallet = new FakeOperatorWallet();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMetrics();
            services.AddSingleton<BettingMetrics>();
            services.AddSingleton<TimeProvider>(TestClock.Create());
            services.AddSingleton(store);
            services.AddSingleton<IOperatorWallet>(wallet);
            services.AddSingleton<IOperatorLimits, BrlLimits>();

            // The live bets of the room are a required dependency of the settlement (SPEC-0025 Emenda v4).
            services.AddSingleton<LiveBetsBatcher>();
            services.AddSingleton<LiveBetsFeed>();
            return new Scenario(services.BuildServiceProvider(), state, wallet);
        }

        /// <summary>An active bet of 10,00 with an automatic cash-out at <paramref name="autoCashOut"/>.</summary>
        public Bet SeedActive(decimal autoCashOut, int panel)
        {
            var round = Bets.Betting(number: 1);
            var bet = Bets.Accepted(Bets.Place(Bets.Request(amount: 10.00m, panel: panel, autoCashOut: autoCashOut), round, Bets.BettingEndsAt.AddSeconds(-3)));
            bet.StartFlight(round.RoundId);
            State.Seed(new AcceptedBet(bet, Sala, BetStatus.Placed, bet.RoundId, bet.RoundNumber, FakeOperatorWallet.OpeningBalance - 10.00m));
            return bet;
        }

        public decimal BalanceAfter(Bet bet) => Wallet.AppliedCredits.Single(c => c.BetId == bet.Id).Result is WalletOk ok ? ok.BalanceAfter.Amount.Value : -1;

        public async Task<CashedOut?> SettleAsync(Bet bet, decimal multiplier)
        {
            var store = _provider.GetRequiredService<IBetStore>();
            var found = await store.FindByIdAsync(bet.Id, TestContext.Current.CancellationToken).ConfigureAwait(false);
            Assert.NotNull(found);
            var settlement = ActivatorUtilities.CreateInstance<CashOutSettlement>(_provider, _provider.GetRequiredService<ILogger<CashOutSettlement>>());
            return await settlement.SettleAsync(found, multiplier, automatic: false).ConfigureAwait(false);
        }

        public void Dispose() => _provider.Dispose();
    }

    /// <summary>The BRL limits of the demo operator (SPEC-0020 seed): max win 100.000,00.</summary>
    private sealed class BrlLimits : IOperatorLimits
    {
        public BetLimits Get(OperatorId op, Currency currency) =>
            new(currency, new Money(1.00m), new Money(1000.00m), new Money(100_000.00m));
    }
}
