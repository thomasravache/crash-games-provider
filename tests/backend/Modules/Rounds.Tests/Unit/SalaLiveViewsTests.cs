using Apogeu.Rounds.Tests.Support;
using Xunit;

namespace Apogeu.Rounds.Tests.Unit;

/// <summary>
/// From the G4 delta review of IT-07 (6a75505): the live view a leader's conductor publishes (<c>SalaLiveViews</c>) is served
/// to <c>JoinSala</c> only while its conductor runs and its lease holds; otherwise the join reads the store. A view is
/// recognised by its round number (published: 1000+), a store read by its read number (1, 2, ...). Tagged with the closest
/// unit test of the snapshot/sync behavior (UT-04): the plan has no UT for IMP-02.
/// </summary>
public sealed class SalaLiveViewsTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "Dada a visão publicada pelo condutor com lease válido, quando um JoinSala chega, então o snapshot vem da visão sem ler o store")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenPublishedViewWithValidLeaseWhenJoinArrivesThenItIsServedWithoutReadingTheStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedRoundStore(gated: false);
        using var harness = new SnapshotQueryHarness(store);
        using var publisher = harness.Open(Lease);

        publisher.Publish(1000);

        Assert.Equal(1000, (await harness.GetAsync(ct)).Current!.RoundNumber);
        Assert.Equal(0, store.Reads);
    }

    [Fact(DisplayName = "Dada a visão publicada, quando o publicador é descartado (condutor parou), então a visão não é mais servida e o JoinSala lê o store")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenPublishedViewWhenPublisherIsDisposedThenTheViewIsNoLongerServed()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedRoundStore(gated: false);
        using var harness = new SnapshotQueryHarness(store);
        var publisher = harness.Open(Lease);
        publisher.Publish(1000);
        Assert.Equal(1000, (await harness.GetAsync(ct)).Current!.RoundNumber);

        publisher.Dispose();

        Assert.Equal(1, (await harness.GetAsync(ct)).Current!.RoundNumber);
        Assert.Equal(1, store.Reads);

        // A publication after the disposal (a conductor still finishing a step) is never served either.
        publisher.Publish(1001);
        Assert.Equal(2, (await harness.GetAsync(ct)).Current!.RoundNumber);
    }

    [Fact(DisplayName = "Dada a visão publicada, quando o lease do mandato expira (FakeTimeProvider), então a visão não é mais servida e o JoinSala lê o store")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenPublishedViewWhenTheLeaseExpiresThenTheViewIsNoLongerServed()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedRoundStore(gated: false);
        using var harness = new SnapshotQueryHarness(store);
        using var publisher = harness.Open(Lease);
        publisher.Publish(1000);

        harness.Clock.Advance(Lease - TimeSpan.FromMilliseconds(1));
        Assert.Equal(1000, (await harness.GetAsync(ct)).Current!.RoundNumber);

        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, (await harness.GetAsync(ct)).Current!.RoundNumber);
        Assert.Equal(1, store.Reads);
    }

    [Fact(DisplayName = "Dada uma publicação concorrente com o descarte do publicador, quando ambos terminam, então a visão publicada nunca é servida")]
    [Trait("Spec", "SPEC-0008:UT-04")]
    public async Task GivenPublishRacingDisposeWhenBothEndThenThePublishedViewIsNeverServed()
    {
        const int Attempts = 500;
        var ct = TestContext.Current.CancellationToken;
        var store = new GatedRoundStore(gated: false);
        using var harness = new SnapshotQueryHarness(store);

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var publisher = harness.Open(Lease);
            using var barrier = new Barrier(2);
            var number = 1000 + attempt;
            var publish = Task.Run(
                () =>
                {
                    barrier.SignalAndWait(ct);
                    publisher.Publish(number);
                },
                ct);
            var dispose = Task.Run(
                () =>
                {
                    barrier.SignalAndWait(ct);
                    publisher.Dispose();
                },
                ct);
            await Task.WhenAll(publish, dispose);

            var snapshot = await harness.GetAsync(ct);
            Assert.True(
                snapshot.Current!.RoundNumber < 1000,
                $"Attempt {attempt}: the view {snapshot.Current.RoundNumber} published concurrently with the disposal of its publisher was served.");
        }

        Assert.Equal(Attempts, store.Reads);
    }
}
