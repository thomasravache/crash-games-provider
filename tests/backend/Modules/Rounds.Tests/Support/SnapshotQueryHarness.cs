using System.Globalization;
using System.Reflection;
using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features;
using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Features.Salas;
using Apogeu.TestKit.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// The real <see cref="SalaSnapshotQuery"/> of the room <c>demo</c> over the real live views (<c>SalaLiveViews</c>) and the
/// real coalesced store read (<c>CoalescedSalaRead</c>), on a given <see cref="IRoundStore"/> and a fake clock (G4 delta
/// review of IT-07). The live views, the leader lease and the query constructor are internal to Rounds.Features, so they
/// are reached by reflection; everything asserted goes through the public <see cref="SalaSnapshotQuery.GetAsync"/>.
/// </summary>
internal sealed class SnapshotQueryHarness : IDisposable
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Assembly Features = typeof(SalaSnapshotQuery).Assembly;
    private static readonly Type LiveViewsType = Features.GetType("Apogeu.Rounds.Features.Live.SalaLiveViews", throwOnError: true)!;
    private static readonly Type LeaseType = Features.GetType("Apogeu.Rounds.Features.Conductor.LeaderLease", throwOnError: true)!;

    private readonly ServiceProvider _provider;
    private readonly object _views;
    private readonly List<CancellationTokenSource> _terms = [];

    public SnapshotQueryHarness(IRoundStore store)
    {
        Store = store;
        var options = new RoundsOptions();
        options.Salas[SalaSettings.SalaId] = new SalaOptions();
        _provider = new ServiceCollection().AddSingleton(store).BuildServiceProvider();
        _views = Activator.CreateInstance(LiveViewsType, nonPublic: true)!;
        Query = (SalaSnapshotQuery)Activator.CreateInstance(
            typeof(SalaSnapshotQuery),
            Instance,
            binder: null,
            [new SalaCatalog(Options.Create(options)), _views, _provider.GetRequiredService<IServiceScopeFactory>()],
            CultureInfo.InvariantCulture)!;
    }

    public FakeTimeProvider Clock { get; } = TestClock.Create();

    public IRoundStore Store { get; }

    public SalaSnapshotQuery Query { get; }

    /// <summary>The snapshot of <c>demo</c>.</summary>
    public async Task<RoundSnapshot> GetAsync(CancellationToken ct) =>
        await Query.GetAsync(SalaSettings.SalaId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException("The room demo has no snapshot.");

    /// <summary>
    /// The publisher of the conductor of <c>demo</c> in a leadership term whose lease ends <paramref name="leaseFor"/> after
    /// now on <see cref="Clock"/>; disposing it withdraws its view.
    /// </summary>
    public LivePublisher Open(TimeSpan leaseFor)
    {
        var term = new CancellationTokenSource();
        _terms.Add(term);
        var lease = Activator.CreateInstance(LeaseType, Instance, binder: null, [Clock.GetUtcNow() + leaseFor, Clock, term], CultureInfo.InvariantCulture)!;
        var publisher = LiveViewsType.GetMethod("Open", Instance)!.Invoke(_views, [SalaSettings.SalaId, lease])!;
        return new LivePublisher((ISalaSnapshotPublisher)publisher, (IDisposable)publisher);
    }

    /// <summary>A published view whose current round has the number <paramref name="number"/>.</summary>
    public static CurrentRound Marker(long number) =>
        new(Guid.NewGuid(), number, RoundState.Betting, $"published-{number}", TestClock.Epoch, null, null, null, null);

    public void Dispose()
    {
        foreach (var term in _terms)
        {
            term.Dispose();
        }

        _provider.Dispose();
    }

    /// <summary>A conductor's publisher of the live views.</summary>
    internal sealed class LivePublisher(ISalaSnapshotPublisher publisher, IDisposable disposable) : IDisposable
    {
        /// <summary>Publishes a view whose current round has the number <paramref name="number"/>.</summary>
        public void Publish(long number) => publisher.Publish(Marker(number), []);

        public void Dispose() => disposable.Dispose();
    }
}
