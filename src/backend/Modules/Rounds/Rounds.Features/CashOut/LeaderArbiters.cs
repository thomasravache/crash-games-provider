using System.Collections.Concurrent;

namespace Apogeu.Rounds.Features.CashOut;

/// <summary>The <see cref="LeaderCashOutArbiter"/> of each room this instance conducts now (one per conductor, SPEC-0023).</summary>
internal sealed class LeaderArbiters
{
    private readonly ConcurrentDictionary<string, LeaderCashOutArbiter> _arbiters = new(StringComparer.Ordinal);

    /// <summary>The arbiter of <paramref name="salaId"/> while its conductor runs here; disposing the result removes it.</summary>
    public IDisposable Lead(string salaId, LeaderCashOutArbiter arbiter)
    {
        _arbiters[salaId] = arbiter;
        return new Term(this, salaId, arbiter);
    }

    public bool TryGet(string salaId, out LeaderCashOutArbiter arbiter) => _arbiters.TryGetValue(salaId, out arbiter!);

    private sealed class Term(LeaderArbiters owner, string salaId, LeaderCashOutArbiter arbiter) : IDisposable
    {
        public void Dispose() => owner._arbiters.TryRemove(new KeyValuePair<string, LeaderCashOutArbiter>(salaId, arbiter));
    }
}
