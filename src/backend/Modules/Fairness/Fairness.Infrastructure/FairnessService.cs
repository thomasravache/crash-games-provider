using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Apogeu.Fairness.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Apogeu.Fairness.Infrastructure;

/// <summary>
/// <see cref="IFairnessService"/> backed by the schema <c>fairness</c>: only the encrypted checkpoints (h_0 and every
/// 10,000 links) and the public data of each chain are stored; any seed is recomputed from its checkpoint.
/// </summary>
internal sealed class FairnessService(
    FairnessDbContext db,
    ChainLinkProtector protector,
    FairnessMetrics metrics,
    ILogger<FairnessService> logger) : IFairnessService
{
    private const int SaltLength = 32;

    public async Task<ChainInfo> CreateChainAsync(SalaId sala, int length, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);

        var chainId = new ChainId(Guid.CreateVersion7());
        var salt = new Salt(RandomNumberGenerator.GetBytes(SaltLength));
        var layout = SeedChain.Build(NewOrigin(), length);

        db.Chains.Add(new ChainRecord
        {
            Id = chainId.Value,
            SalaId = sala.Value,
            Length = length,
            IssuedRounds = 0,
            TerminalHash = layout.TerminalHash.ToArray(),
            Salt = salt.ToArray(),
        });
        foreach (var checkpoint in layout.Checkpoints)
        {
            db.Checkpoints.Add(new CheckpointRecord
            {
                ChainId = chainId.Value,
                LinkIndex = checkpoint.LinkIndex,
                ProtectedLink = protector.Protect(chainId, checkpoint.LinkIndex, checkpoint.Link),
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        db.ChangeTracker.Clear();

        FairnessLog.ChainCreated(logger, chainId.Value, sala.Value, length);
        metrics.ChainCreated(sala, length);
        return new ChainInfo(chainId, layout.TerminalHash, salt, length);
    }

    public async Task<RoundSeedCommitment> NextRoundAsync(SalaId sala, CancellationToken ct)
    {
        // One transaction: the round index is consumed only together with the commitment it produces, so a failure
        // neither skips nor repeats a seed. The row lock serialises concurrent calls of the same room.
        var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var issued = await db.Database.SqlQuery<IssuedRound>($"""
                UPDATE fairness.chains
                SET issued_rounds = issued_rounds + 1
                WHERE id = (
                    SELECT active.id FROM fairness.chains AS active
                    WHERE active.sala_id = {sala.Value}
                    ORDER BY active.sequence DESC
                    LIMIT 1)
                  AND issued_rounds < length
                RETURNING id AS "ChainId", issued_rounds AS "RoundIndex", length AS "Length"
                """).ToListAsync(ct).ConfigureAwait(false);

            if (issued.Count == 0)
            {
                FairnessLog.ChainExhausted(logger, sala.Value);
                throw new ChainExhaustedException("The room has no round left in an active seed chain; create a new chain.");
            }

            var round = issued[0];
            var chainId = new ChainId(round.ChainId);
            var seed = await LinkAtAsync(chainId, SeedChain.LinkIndexOfRound(round.Length, round.RoundIndex), ct).ConfigureAwait(false);
            var commitment = SeedChain.Next(seed);

            await transaction.CommitAsync(ct).ConfigureAwait(false);

            var remaining = round.Length - round.RoundIndex;
            FairnessLog.RoundIssued(logger, round.RoundIndex, chainId.Value, sala.Value, remaining);
            metrics.RoundIssued(sala, remaining);
            return new RoundSeedCommitment(chainId, round.RoundIndex, commitment);
        }
    }

    public async Task<RevealedRound> RevealAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct)
    {
        var record = await db.Chains.AsNoTracking()
            .Where(c => c.Id == chain.Value)
            .Select(c => new { c.Length, c.IssuedRounds, c.Salt })
            .SingleOrDefaultAsync(ct).ConfigureAwait(false);

        // Only issued rounds are revealed: revealing s_r of a future round would expose every later outcome (ADR-0006).
        if (record is null || roundIndex < 1 || roundIndex > record.IssuedRounds)
        {
            throw new RoundNotFoundException("The round does not exist or was not issued yet.");
        }

        var seed = await LinkAtAsync(chain, SeedChain.LinkIndexOfRound(record.Length, roundIndex), ct).ConfigureAwait(false);
        var salt = new Salt(record.Salt);
        var crashPoint = CrashPointCalculator.Compute(seed, salt, rtp, cap);

        FairnessLog.RoundRevealed(logger, roundIndex, chain.Value);
        return new RevealedRound(seed, SeedChain.Next(seed), salt, crashPoint);
    }

    public CrashPoint ComputeCrashPoint(Seed seed, Salt salt, Rtp rtp, Multiplier cap) =>
        CrashPointCalculator.Compute(seed, salt, rtp, cap);

    public VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed) =>
        FairnessVerifier.Verify(seed, previousSeed, salt, rtp, cap, claimed);

    private static Seed NewOrigin()
    {
        var bytes = RandomNumberGenerator.GetBytes(Seed.Length);
        try
        {
            return new Seed(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>The link h_k: decrypts the nearest checkpoint below it and hashes forward (fewer than 10,000 hashes).</summary>
    private async Task<Seed> LinkAtAsync(ChainId chain, int linkIndex, CancellationToken ct)
    {
        var checkpointIndex = SeedChain.CheckpointOf(linkIndex);
        var protectedLink = await db.Checkpoints.AsNoTracking()
            .Where(c => c.ChainId == chain.Value && c.LinkIndex == checkpointIndex)
            .Select(c => c.ProtectedLink)
            .SingleAsync(ct).ConfigureAwait(false);

        var checkpoint = protector.Unprotect(chain, checkpointIndex, protectedLink);
        return SeedChain.Advance(checkpoint, linkIndex - checkpointIndex);
    }
}
