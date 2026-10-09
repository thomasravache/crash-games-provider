using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Microsoft.EntityFrameworkCore;

namespace Apogeu.Rounds.Infrastructure.Persistence;

/// <summary><see cref="IRoundStore"/> and <see cref="IRoundChainStore"/> on the schema <c>rounds</c>; reads are not tracked.</summary>
internal sealed class RoundStore(RoundsDbContext db) : IRoundStore, IRoundChainStore
{
    public async Task<Round?> FindAsync(RoundId id, CancellationToken ct)
    {
        var record = await db.Rounds.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id.Value, ct).ConfigureAwait(false);
        return record is null ? null : ToDomain(record);
    }

    public async Task<Round?> FindLatestAsync(string salaId, CancellationToken ct)
    {
        var record = await db.Rounds.AsNoTracking()
            .Where(r => r.SalaId == salaId)
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<Round>> ListCrashedAsync(string salaId, int limit, CancellationToken ct)
    {
        var crashed = StateName(RoundState.Crashed);
        var records = await db.Rounds.AsNoTracking()
            .Where(r => r.SalaId == salaId && r.State == crashed)
            .OrderByDescending(r => r.Number)
            .Take(limit)
            .ToListAsync(ct).ConfigureAwait(false);
        return records.Select(ToDomain).ToArray();
    }

    public async Task SaveAsync(Round round, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(round);

        var record = await db.Rounds.SingleOrDefaultAsync(r => r.Id == round.Id.Value, ct).ConfigureAwait(false);
        if (record is null)
        {
            record = new RoundRecord { Id = round.Id.Value };
            db.Rounds.Add(record);
        }

        record.SalaId = round.SalaId;
        record.Number = round.Number;
        record.ChainId = round.ChainId;
        record.ChainRoundIndex = round.ChainRoundIndex;
        record.Commitment = round.Commitment;
        record.State = StateName(round.State);
        record.BettingEndsAt = round.BettingEndsAt;
        record.StartedAt = round.StartedAt;
        record.CrashedAt = round.CrashedAt;
        record.CrashPoint = round.CrashPoint;
        record.RevealedSeed = round.RevealedSeed;
        record.AnnulReason = round.AnnulReason;
        record.AnnulledAt = round.AnnulledAt;

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            // The store outlives many rounds of a conductor: nothing stays tracked between saves.
            db.ChangeTracker.Clear();
        }
    }

    async Task<RoundChain?> IRoundChainStore.FindAsync(Guid chainId, CancellationToken ct)
    {
        var record = await db.Chains.AsNoTracking().SingleOrDefaultAsync(c => c.Id == chainId, ct).ConfigureAwait(false);
        return record is null
            ? null
            : new RoundChain(record.Id, record.SalaId, record.TerminalHash, record.Salt, record.Length, record.CreatedAt);
    }

    public async Task AddAsync(RoundChain chain, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(chain);

        if (await db.Chains.AsNoTracking().AnyAsync(c => c.Id == chain.ChainId, ct).ConfigureAwait(false))
        {
            return;
        }

        db.Chains.Add(new ChainRecord
        {
            Id = chain.ChainId,
            SalaId = chain.SalaId,
            TerminalHash = chain.TerminalHash,
            Salt = chain.Salt,
            Length = chain.Length,
            CreatedAt = chain.CreatedAt,
        });
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private static Round ToDomain(RoundRecord record) => Round.Restore(
        new RoundId(record.Id),
        record.SalaId,
        record.Number,
        record.ChainId,
        record.ChainRoundIndex,
        record.Commitment,
        ParseState(record.State),
        record.BettingEndsAt,
        record.StartedAt,
        record.CrashedAt,
        record.CrashPoint,
        record.RevealedSeed,
        record.AnnulReason,
        record.AnnulledAt);

    private static string StateName(RoundState state) => state switch
    {
        RoundState.Betting => "betting",
        RoundState.Flying => "flying",
        RoundState.Crashed => "crashed",
        RoundState.Annulled => "annulled",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown round state."),
    };

    private static RoundState ParseState(string state) => state switch
    {
        "betting" => RoundState.Betting,
        "flying" => RoundState.Flying,
        "crashed" => RoundState.Crashed,
        "annulled" => RoundState.Annulled,
        _ => throw new InvalidOperationException($"Unknown round state '{state}' in the schema rounds."),
    };
}
