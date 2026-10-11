using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;

namespace Apogeu.Betting.Tests.Support;

/// <summary>Decorator of the Host's (scoped) <see cref="IRoundStore"/> whose <c>FindLatestAsync</c> passes a <see cref="ResumeGate"/>.</summary>
internal sealed class GatedRoundStore(IRoundStore inner, ResumeGate gate) : IRoundStore
{
    public Task<Round?> FindAsync(RoundId id, CancellationToken ct) => inner.FindAsync(id, ct);

    public async Task<Round?> FindLatestAsync(string salaId, CancellationToken ct)
    {
        await gate.PassAsync().ConfigureAwait(false);
        return await inner.FindLatestAsync(salaId, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<Round>> ListCrashedAsync(string salaId, int limit, CancellationToken ct) => inner.ListCrashedAsync(salaId, limit, ct);

    public Task SaveAsync(Round round, CancellationToken ct) => inner.SaveAsync(round, ct);
}
