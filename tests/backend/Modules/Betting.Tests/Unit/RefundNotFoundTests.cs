using Apogeu.Betting.Domain;
using Apogeu.Betting.Features;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.Settlement;
using Apogeu.Betting.Infrastructure.Messaging;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.Betting.Tests.Support;
using Apogeu.Rounds.Contracts;
using Apogeu.TestKit.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

/// <summary>
/// The refund of a bet of an annulled round answered <c>TransactionNotFound</c> (SPEC-0024 §6, review G4): the wallet has no debit
/// to reverse, so nothing was refunded — 0,00, a reconciliation alert, and 0,00 refunded in the round's summary.
/// </summary>
public sealed class RefundNotFoundTests
{
    private const string Sala = "demo";

    [Fact(DisplayName = "Dado um estorno respondido com TransactionNotFound, quando registrado, então BetRefunded informa 0,00 estornado e o alerta de conciliação nomeia a aposta")]
    [Trait("Spec", "SPEC-0024:UT-04")]
    public async Task GivenRefundAnsweredTransactionNotFoundWhenRecordedThenZeroRefundedAndAlert()
    {
        var ct = TestContext.Current.CancellationToken;
        var round = Bets.Betting(number: 1);
        var bet = Bets.Accepted(Bets.Place(Bets.Request(amount: 10.00m, panel: 1), round, Bets.BettingEndsAt.AddSeconds(-3)));
        bet.StartFlight(round.RoundId);
        Assert.IsType<BetClosedRefunded>(bet.Refund());
        var (store, state) = InMemoryBetStore.Create();
        state.Seed(new AcceptedBet(bet, Sala, BetStatus.Placed, bet.RoundId, bet.RoundNumber, FakeOperatorWallet.OpeningBalance - 10.00m, Refund: new BetRefund(10.00m, Confirmed: false, Balance: null)));
        var (hub, sent) = RecordingHubContext.Create();
        using var logs = new LogCapture();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        services.AddMetrics();
        services.AddSingleton<BettingMetrics>();
        await using var provider = services.BuildServiceProvider();

        // The wallet never saw the debit of this bet: its rollback is TransactionNotFound (SPEC-0021).
        var wallet = new FakeOperatorWallet();
        await RefundBetHandler.HandleAsync(
            new RefundBet(bet.Id, Bets.Player.OperatorId.Value, Bets.Player.PlayerId.Value, Bets.Player.Currency.Code, round.RoundId.Value, 10.00m),
            store,
            wallet,
            new BetsNotifier(hub),
            provider.GetRequiredService<BettingMetrics>(),
            new TestMessageContext(),
            provider.GetRequiredService<ILogger<RefundBet>>(),
            ct);

        Assert.Contains(wallet.Calls, c => c.Result is Apogeu.Wallet.Contracts.WalletTransactionNotFound);
        var refunded = Assert.IsType<BetRefunded>(Assert.Single(sent.Sent, s => string.Equals(s.Target, "BetRefunded", StringComparison.Ordinal)).Message);
        Assert.True(refunded.Refunded.Value == 0.00m, $"Nothing was refunded: BetRefunded must say 0,00, got {refunded}.");
        Assert.Equal(RefundStatus.Refunded, refunded.RefundStatus);
        Assert.Equal(FakeOperatorWallet.OpeningBalance, refunded.Balance?.Value);

        var betId = bet.Id.ToString();
        Assert.True(
            logs.Entries.Any(e => e.Level >= LogLevel.Warning && e.Message.Contains(betId, StringComparison.OrdinalIgnoreCase)),
            $"Expected a reconciliation alert (log ≥ Warning naming bet {betId}).");
        Assert.NotEmpty(state.RefundConfirmations);
    }

    [Fact(DisplayName = "Dada uma aposta refunded cujo estorno respondeu TransactionNotFound (0,00 estornado), quando o resumo é calculado, então conta 0,00 estornado para ela")]
    [Trait("Spec", "SPEC-0024:UT-04")]
    public void GivenBetRefundedWithNothingWhenSummarizedThenZeroRefundedIsCounted()
    {
        var round = Bets.Betting(number: 1);
        var nothing = RestoreRefunded(round, 10.00m, refunded: 0.00m);
        var full = RestoreRefunded(round, 20.00m, refunded: 20.00m);

        var summary = Assert.Single(RoundSettlementCalculator.Summarize([nothing, full]));

        Assert.Equal(30.00m, summary.TotalWagered);
        Assert.Equal(20.00m, summary.TotalRefunded);
        Assert.Equal(10.00m, summary.Ggr);
    }

    /// <summary>
    /// Seam of review G4 (m3): a refunded bet restored with the amount its refund actually returned —
    /// <c>Bet.Restore(Guid, BetOwner, BetRequest, BetStatus, RoundId?, long?, decimal? cashOutMultiplier, decimal? payout,
    /// decimal? refunded)</c>, bound by reflection so the suite compiles before it exists; <c>Summarize</c> sums it.
    /// </summary>
    private static Bet RestoreRefunded(RoundWindow round, decimal amount, decimal refunded)
    {
        var restore = typeof(Bet).GetMethod(
            nameof(Bet.Restore),
            [typeof(Guid), typeof(BetOwner), typeof(BetRequest), typeof(BetStatus), typeof(RoundId?), typeof(long?), typeof(decimal?), typeof(decimal?), typeof(decimal?)]);
        Assert.True(
            restore is not null,
            "Bet has no Restore(..., decimal? cashOutMultiplier, decimal? payout, decimal? refunded): a refund that returned 0,00 cannot be told from a full one.");
        return (Bet)restore.Invoke(null, [Guid.NewGuid(), Bets.Player, Bets.Request(amount: amount), BetStatus.Refunded, round.RoundId, round.RoundNumber, null, null, refunded])!;
    }
}
