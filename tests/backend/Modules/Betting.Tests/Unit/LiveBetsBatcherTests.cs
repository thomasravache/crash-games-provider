using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.LiveBets;
using Apogeu.Betting.Tests.Support;
using Apogeu.BuildingBlocks.Contracts;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

/// <summary>
/// UT-02 (SPEC-0025 §4 and §6, ADR-0019): the live bets of a room are grouped in windows of 200 ms on the injected clock,
/// each batch with at most 100 bets (the highest amounts) and 100 cash-outs, the rest counting only in the totals.
/// </summary>
public sealed class LiveBetsBatcherTests
{
    private const string Sala = "demo";

    private static readonly DateTimeOffset Start = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Dadas 250 apostas na mesma janela de 200 ms, quando o lote é montado, então traz 100 itens (maiores valores) e totais de 250")]
    [Trait("Spec", "SPEC-0025:UT-02")]
    public void GivenTwoHundredFiftyBetsInOneWindowWhenBatchedThenHundredHighestItemsAndTotalsOfTwoHundredFifty()
    {
        var clock = new FakeTimeProvider(Start);
        var batcher = new LiveBetsBatcher(clock);
        var round = Guid.NewGuid();

        // 250 bets of 1,00 to 250,00 from 125 players (two each), half of them with a nickname, within 200 ms.
        var placed = Enumerable.Range(1, 250).Select(i => Placed(round, Player(i % 125), i % 2 == 0 ? "Marcos" : null, i)).ToList();
        foreach (var bet in placed)
        {
            batcher.Record(bet);
        }

        clock.Advance(LiveBetsBatcher.Window);
        var due = batcher.TakeDue();

        var batch = Assert.Single(due);
        Assert.Equal(Sala, batch.SalaId);
        Assert.Equal(round, batch.Batch.RoundId);
        Assert.Equal(LiveBetsBatcher.MaxItems, batch.Batch.Placed.Count);
        Assert.Equal(Enumerable.Range(151, 100).Reverse().Select(i => (decimal)i), batch.Batch.Placed.Select(b => b.Amount.Value));
        Assert.Empty(batch.Batch.CashedOut);
        Assert.Equal(250, batch.Batch.TotalBets);
        Assert.Equal(125, batch.Batch.TotalPlayers);
        Assert.Equal(Enumerable.Range(1, 250).Sum(i => (decimal)i), batch.Batch.TotalWagered.Value);
        AssertRoomView(batch.Batch.Placed, placed);
        Assert.Equal(100, batch.Batch.Placed.Select(b => b.BetRef).Distinct(StringComparer.Ordinal).Count());

        // The news was taken: nothing else is due in the same instant.
        Assert.Empty(batcher.TakeDue());

        // The snapshot of the round: the same 100 highest bets and the totals of the 250.
        var snapshot = batcher.SnapshotOf(Sala);
        Assert.NotNull(snapshot);
        Assert.Equal(round, snapshot.RoundId);
        Assert.Equal(250, snapshot.TotalBets);
        Assert.Equal(125, snapshot.TotalPlayers);
        Assert.Equal(batch.Batch.TotalWagered, snapshot.TotalWagered);
        Assert.Equal(batch.Batch.Placed.Select(b => (b.BetRef, b.Amount)), snapshot.Bets.Select(b => (b.BetRef, b.Amount)));
        Assert.Null(batcher.SnapshotOf("another-sala"));
    }

    [Fact(DisplayName = "Dado um lote enviado, quando chegam aposta e cash-out 100 ms depois, então nada sai antes de completar 200 ms e o lote seguinte traz ambos com o mesmo betRef")]
    [Trait("Spec", "SPEC-0025:UT-02")]
    public void GivenBatchSentWhenNewsArrivesThenNextBatchOnlyAfterTheWindowWithCashOutOfTheSameBetRef()
    {
        var clock = new FakeTimeProvider(Start);
        var batcher = new LiveBetsBatcher(clock);
        var round = Guid.NewGuid();
        var first = Placed(round, Player(1), "Marcos", 10);
        batcher.Record(first);
        clock.Advance(LiveBetsBatcher.Window);
        var firstRef = Assert.Single(Assert.Single(batcher.TakeDue()).Batch.Placed).BetRef;

        var second = Placed(round, Player(2), "Beatriz", 15);
        batcher.Record(second);
        batcher.Record(new LiveBetCashedOut(Sala, round, first.BetId, new Multiplier(1.50m), new Money(15.00m)));

        // At most one batch per room every 200 ms.
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Empty(batcher.TakeDue());
        clock.Advance(TimeSpan.FromMilliseconds(100));
        var next = Assert.Single(batcher.TakeDue()).Batch;

        Assert.Equal(round, next.RoundId);
        var bet = Assert.Single(next.Placed);
        Assert.Equal("Be***", bet.Nickname);
        Assert.Equal(15.00m, bet.Amount.Value);
        Assert.False(string.Equals(firstRef, bet.BetRef, StringComparison.Ordinal), "Each bet has its own betRef.");
        var cashOut = Assert.Single(next.CashedOut);
        Assert.Equal(firstRef, cashOut.BetRef);
        Assert.Equal(1.50m, cashOut.Multiplier.Value);
        Assert.Equal(15.00m, cashOut.Payout.Value);
        Assert.Equal(2, next.TotalBets);
        Assert.Equal(2, next.TotalPlayers);
        Assert.Equal(25.00m, next.TotalWagered.Value);

        // The snapshot shows the cash-out on the bet itself.
        var snapshot = batcher.SnapshotOf(Sala);
        Assert.NotNull(snapshot);
        var cashed = Assert.Single(snapshot.Bets, b => string.Equals(b.BetRef, firstRef, StringComparison.Ordinal));
        Assert.Equal(1.50m, cashed.Multiplier?.Value);
        Assert.Equal(15.00m, cashed.Payout?.Value);
        var open = Assert.Single(snapshot.Bets, b => !string.Equals(b.BetRef, firstRef, StringComparison.Ordinal));
        Assert.Null(open.Multiplier);
        Assert.Null(open.Payout);
        Assert.Equal([15.00m, 10.00m], snapshot.Bets.Select(b => b.Amount.Value));
    }

    [Fact(DisplayName = "Dados 150 cash-outs na mesma janela, quando o lote é montado, então traz no máximo 100 cash-outs")]
    [Trait("Spec", "SPEC-0025:UT-02")]
    public void GivenHundredFiftyCashOutsInOneWindowWhenBatchedThenAtMostHundredCashOuts()
    {
        var clock = new FakeTimeProvider(Start);
        var batcher = new LiveBetsBatcher(clock);
        var round = Guid.NewGuid();
        var placed = Enumerable.Range(1, 150).Select(i => Placed(round, Player(i), null, 1)).ToList();
        foreach (var bet in placed)
        {
            batcher.Record(bet);
        }

        clock.Advance(LiveBetsBatcher.Window);
        _ = batcher.TakeDue();
        foreach (var bet in placed)
        {
            batcher.Record(new LiveBetCashedOut(Sala, round, bet.BetId, new Multiplier(1.20m), new Money(1.20m)));
        }

        clock.Advance(LiveBetsBatcher.Window);
        var batch = Assert.Single(batcher.TakeDue()).Batch;

        Assert.Empty(batch.Placed);
        Assert.Equal(LiveBetsBatcher.MaxItems, batch.CashedOut.Count);
        Assert.Equal(150, batch.TotalBets);
        Assert.Equal(150, batch.TotalPlayers);
        Assert.All(batch.CashedOut, c => LiveBetsPrivacy.AssertOpaqueRef(c.BetRef, placed.Select(p => p.BetId), "LiveCashOut"));
    }

    [Fact(DisplayName = "Dada uma nova rodada da sala, quando a primeira aposta dela é registrada, então os totais recomeçam")]
    [Trait("Spec", "SPEC-0025:UT-02")]
    public void GivenNewRoundWhenItsFirstBetIsRecordedThenTotalsRestart()
    {
        var clock = new FakeTimeProvider(Start);
        var batcher = new LiveBetsBatcher(clock);
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        batcher.Record(Placed(first, Player(1), "Marcos", 10));
        batcher.Record(Placed(first, Player(2), "Beatriz", 20));
        clock.Advance(LiveBetsBatcher.Window);
        _ = batcher.TakeDue();

        batcher.Record(Placed(second, Player(3), "Carla", 5));
        clock.Advance(LiveBetsBatcher.Window);
        var batch = Assert.Single(batcher.TakeDue()).Batch;

        Assert.Equal(second, batch.RoundId);
        Assert.Equal(1, batch.TotalBets);
        Assert.Equal(1, batch.TotalPlayers);
        Assert.Equal(5.00m, batch.TotalWagered.Value);
        var snapshot = batcher.SnapshotOf(Sala);
        Assert.NotNull(snapshot);
        Assert.Equal(second, snapshot.RoundId);
        Assert.Equal("Ca***", Assert.Single(snapshot.Bets).Nickname);
    }

    private static void AssertRoomView(IReadOnlyList<LiveBet> bets, IReadOnlyList<LiveBetPlaced> placed)
    {
        var ids = placed.Select(p => p.BetId).ToList();
        foreach (var bet in bets)
        {
            var source = Assert.Single(placed, p => p.Amount == bet.Amount);
            Assert.Equal(LiveBetNickname.Anonymize(source.Nickname), bet.Nickname);
            Assert.Contains(bet.Nickname, (string[])["Ma***", "Jogador***"], StringComparer.Ordinal);
            Assert.Equal("BRL", bet.Currency);
            Assert.Null(bet.Multiplier);
            Assert.Null(bet.Payout);
            LiveBetsPrivacy.AssertOpaqueRef(bet.BetRef, ids, "LiveBet");
        }
    }

    private static LiveBetPlaced Placed(Guid round, BetOwner owner, string? nickname, decimal amount) =>
        new(Sala, round, Guid.NewGuid(), owner, nickname, new Money(amount));

    private static BetOwner Player(int number) =>
        new(new OperatorId("demo"), new PlayerId("player-" + number.ToString(System.Globalization.CultureInfo.InvariantCulture)), new Currency("BRL"));
}
