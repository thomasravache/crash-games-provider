using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// The snapshot reads (<c>FindLatestAsync</c>) one Host makes through its real <see cref="IRoundStore"/>: how many, and how
/// many at the same time (G4 delta review of IT-07).
/// </summary>
internal sealed class RoundStoreReads
{
    private int _reads;
    private int _active;
    private int _maxActive;

    public int Reads => Volatile.Read(ref _reads);

    public int MaxConcurrent => Volatile.Read(ref _maxActive);

    /// <summary>Replaces the Host's <see cref="IRoundStore"/> with the real one wrapped in a counter of its reads.</summary>
    public void Decorate(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var real = services.Last(d => d.ServiceType == typeof(IRoundStore));
        var factory = real.ImplementationFactory ?? throw new InvalidOperationException("IRoundStore is expected to be registered by a factory.");
        services.Remove(real);
        services.AddScoped<IRoundStore>(provider => new CountingRoundStore((IRoundStore)factory(provider), this));
    }

    private Exit Enter()
    {
        Interlocked.Increment(ref _reads);
        var active = Interlocked.Increment(ref _active);
        var current = Volatile.Read(ref _maxActive);
        while (active > current)
        {
            var seen = Interlocked.CompareExchange(ref _maxActive, active, current);
            if (seen == current)
            {
                break;
            }

            current = seen;
        }

        return new Exit(this);
    }

    private sealed class Exit(RoundStoreReads reads) : IDisposable
    {
        public void Dispose() => Interlocked.Decrement(ref reads._active);
    }

    private sealed class CountingRoundStore(IRoundStore inner, RoundStoreReads reads) : IRoundStore
    {
        public async Task<Round?> FindLatestAsync(string salaId, CancellationToken ct)
        {
            using (reads.Enter())
            {
                return await inner.FindLatestAsync(salaId, ct).ConfigureAwait(false);
            }
        }

        public Task<Round?> FindAsync(RoundId id, CancellationToken ct) => inner.FindAsync(id, ct);

        public Task<IReadOnlyList<Round>> ListCrashedAsync(string salaId, int limit, CancellationToken ct) => inner.ListCrashedAsync(salaId, limit, ct);

        public Task SaveAsync(Round round, CancellationToken ct) => inner.SaveAsync(round, ct);
    }
}
