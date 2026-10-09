using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>In-memory <see cref="IRoundStore"/> for the unit tests of the conductor (the real one is PostgreSQL, IT-*).</summary>
internal sealed class InMemoryRoundStore : IRoundStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<RoundId, Round> _rounds = [];

    public Task<Round?> FindAsync(RoundId id, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult(_rounds.GetValueOrDefault(id));
        }
    }

    public Task<Round?> FindLatestAsync(string salaId, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult(_rounds.Values
                .Where(r => string.Equals(r.SalaId, salaId, StringComparison.Ordinal))
                .MaxBy(r => r.Number));
        }
    }

    public Task<IReadOnlyList<Round>> ListCrashedAsync(string salaId, int limit, CancellationToken ct)
    {
        lock (_gate)
        {
            IReadOnlyList<Round> crashed = [.. _rounds.Values
                .Where(r => string.Equals(r.SalaId, salaId, StringComparison.Ordinal) && r.State == RoundState.Crashed)
                .OrderByDescending(r => r.Number)
                .Take(limit)];
            return Task.FromResult(crashed);
        }
    }

    public Task SaveAsync(Round round, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(round);
        lock (_gate)
        {
            _rounds[round.Id] = round;
        }

        return Task.CompletedTask;
    }
}
