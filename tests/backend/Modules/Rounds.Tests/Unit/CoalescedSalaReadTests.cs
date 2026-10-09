using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Rounds.Tests.Unit;

/// <summary>
/// From the G4 delta review of IT-07 (6a75505): the store read shared by the joins of an instance that has no live view of
/// the room (<c>CoalescedSalaRead</c>, through <see cref="SalaSnapshotQuery"/>). The plan has no UT for IMP-02, so these
/// guards are tagged with the closest unit test of the snapshot/sync behavior (UT-04); IT-07 tags only the integration.
/// </summary>
public sealed class CoalescedSalaReadTests
{
    private const int Callers = 50;

    [Fact(DisplayName = "Dados N JoinSala que chegam enquanto uma leitura do store roda, quando ela termina, então os N compartilham uma única leitura seguinte e nunca há duas leituras ao mesmo tempo")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenCallersArrivingDuringAReadWhenItEndsThenTheyShareExactlyOneNextRead()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedRoundStore();
        using var harness = new SnapshotQueryHarness(store);

        var first = harness.GetAsync(ct);
        await store.StartedAsync(1);
        var batch = Enumerable.Range(0, Callers).Select(_ => harness.GetAsync(ct)).ToArray();
        Assert.Equal(1, store.Reads);

        store.Release(1);
        Assert.Equal(1, (await first).Current!.RoundNumber);
        await store.StartedAsync(2);
        store.Release(2);
        var snapshots = await Task.WhenAll(batch);

        Assert.All(snapshots, s => Assert.Equal(2, s.Current!.RoundNumber));
        Assert.True(store.Reads == 2, $"{Callers + 1} joins in two batches must cost 2 store reads, not {store.Reads}.");
        Assert.Equal(1, store.MaxConcurrentReads);
    }

    [Fact(DisplayName = "Dado um JoinSala que chega durante uma leitura do store, quando essa leitura termina, então ele espera a próxima leitura e nunca recebe a mais antiga")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenCallerArrivingDuringAReadWhenItEndsThenItWaitsForTheNextRead()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedRoundStore();
        using var harness = new SnapshotQueryHarness(store);

        var first = harness.GetAsync(ct);
        await store.StartedAsync(1);
        var second = harness.GetAsync(ct);

        store.Release(1);
        Assert.Equal(1, (await first).Current!.RoundNumber);
        await store.StartedAsync(2);
        var third = harness.GetAsync(ct);
        Assert.False(second.IsCompleted, "A join that arrived during read 1 was answered before read 2 ended.");

        store.Release(2);
        Assert.Equal(2, (await second).Current!.RoundNumber);
        await store.StartedAsync(3);
        Assert.False(third.IsCompleted, "A join that arrived during read 2 was answered by it.");

        store.Release(3);
        Assert.Equal(3, (await third).Current!.RoundNumber);
    }

    [Fact(DisplayName = "Dada uma leitura do store que falha, quando os JoinSala do lote a aguardam, então todos recebem a falha e a leitura seguinte volta a responder")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenAFailingReadWhenItsBatchAwaitsThenEveryCallerObservesTheFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedRoundStore();
        using var harness = new SnapshotQueryHarness(store);
        var failure = new InvalidOperationException("read 2 failed");

        var first = harness.GetAsync(ct);
        await store.StartedAsync(1);
        var batch = Enumerable.Range(0, Callers).Select(_ => harness.GetAsync(ct)).ToArray();
        store.Release(1);
        await first;
        await store.StartedAsync(2);
        store.Fail(2, failure);

        foreach (var caller in batch)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => caller);
            Assert.Same(failure, error);
        }

        var after = harness.GetAsync(ct);
        await store.StartedAsync(3);
        store.Release(3);
        Assert.Equal(3, (await after).Current!.RoundNumber);
    }

    [Fact(DisplayName = "Dados dois JoinSala no mesmo lote, quando um deles é cancelado durante a leitura, então a leitura compartilhada não é cancelada e o outro recebe o snapshot")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenOneCallerCancelledWhenTheSharedReadRunsThenTheReadGoesOnForTheOthers()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedRoundStore();
        using var harness = new SnapshotQueryHarness(store);
        using var leaving = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var first = harness.GetAsync(ct);
        await store.StartedAsync(1);
        var cancelled = harness.GetAsync(leaving.Token);
        var staying = harness.GetAsync(ct);
        store.Release(1);
        await first;
        await store.StartedAsync(2);

        await leaving.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.False(store.TokenOf(2).IsCancellationRequested, "The cancellation of one join reached the read shared by its batch.");

        store.Release(2);
        Assert.Equal(2, (await staying).Current!.RoundNumber);
        Assert.Equal(2, store.Reads);
    }

    // Revisão G4 do delta: prazo da leitura compartilhada.
    [Fact(DisplayName = "Dada uma leitura do store que não termina, quando o prazo da leitura compartilhada vence, então todo JoinSala do lote recebe TimeoutException e o JoinSala seguinte dispara uma nova leitura que responde")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenAStalledReadWhenItsDeadlineExpiresThenTheBatchTimesOutAndTheNextCallerReadsAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedRoundStore();
        using var harness = new SnapshotQueryHarness(store, TimeSpan.FromMilliseconds(100));

        var first = harness.GetAsync(ct);
        await store.StartedAsync(1);
        var batch = Enumerable.Range(0, Callers).Select(_ => harness.GetAsync(ct)).ToArray();
        store.Release(1);
        Assert.Equal(1, (await first).Current!.RoundNumber);
        await store.StartedAsync(2);

        foreach (var caller in batch)
        {
            await Assert.ThrowsAsync<TimeoutException>(() => caller.WaitAsync(TimeSpan.FromSeconds(10), ct));
        }

        Assert.True(store.TokenOf(2).IsCancellationRequested, "The deadline did not reach the stalled store read.");
        Assert.Equal(2, store.Reads);

        var after = harness.GetAsync(ct);
        await store.StartedAsync(3);
        store.Release(3);
        Assert.Equal(3, (await after.WaitAsync(TimeSpan.FromSeconds(10), ct)).Current!.RoundNumber);
        Assert.Equal(3, store.Reads);
        Assert.Equal(1, store.MaxConcurrentReads);
    }

    // Revisão G4 do delta: prazo da leitura compartilhada.
    [Fact(DisplayName = "Dado um lote cuja leitura não termina, quando um JoinSala cancela antes do prazo, então só ele recebe o cancelamento, a leitura compartilhada segue e os demais recebem o TimeoutException do lote")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenAStalledReadWhenOneCallerCancelsBeforeTheDeadlineThenOnlyItIsCancelledAndTheOthersTimeOut()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedRoundStore();
        using var harness = new SnapshotQueryHarness(store, TimeSpan.FromSeconds(1));
        using var leaving = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var first = harness.GetAsync(ct);
        await store.StartedAsync(1);
        var cancelled = harness.GetAsync(leaving.Token);
        var staying = Enumerable.Range(0, Callers).Select(_ => harness.GetAsync(ct)).ToArray();
        store.Release(1);
        await first;
        await store.StartedAsync(2);

        await leaving.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(TimeSpan.FromSeconds(10), ct));
        Assert.False(store.TokenOf(2).IsCancellationRequested, "The cancellation of one join reached the read shared by its batch.");
        Assert.All(staying, caller => Assert.False(caller.IsCompleted, "The cancellation of one join completed the others."));

        foreach (var caller in staying)
        {
            await Assert.ThrowsAsync<TimeoutException>(() => caller.WaitAsync(TimeSpan.FromSeconds(10), ct));
        }

        Assert.True(store.TokenOf(2).IsCancellationRequested, "The deadline did not reach the stalled store read.");
        Assert.Equal(2, store.Reads);
    }
}
