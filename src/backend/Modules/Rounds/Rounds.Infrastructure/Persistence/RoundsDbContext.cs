using Microsoft.EntityFrameworkCore;

namespace Apogeu.Rounds.Infrastructure.Persistence;

/// <summary>The DbContext of the Rounds module: it maps only the schema <c>rounds</c> (ADR-0010).</summary>
internal sealed class RoundsDbContext(DbContextOptions<RoundsDbContext> options) : DbContext(options)
{
    public const string Schema = "rounds";

    public DbSet<RoundRecord> Rounds => Set<RoundRecord>();

    public DbSet<ChainRecord> Chains => Set<ChainRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<RoundRecord>(round =>
        {
            round.ToTable("rounds", table =>
            {
                table.HasCheckConstraint("ck_rounds_state", "state IN ('betting', 'flying', 'crashed', 'annulled')");
                table.HasCheckConstraint("ck_rounds_number", "number >= 1");

                // The outcome exists only after the crash (SPEC-0008 §4).
                table.HasCheckConstraint(
                    "ck_rounds_outcome_after_crash",
                    "(state = 'crashed') = (crash_point IS NOT NULL AND revealed_seed IS NOT NULL AND crashed_at IS NOT NULL)");
            });
            round.HasKey(r => r.Id).HasName("pk_rounds");
            round.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
            round.Property(r => r.SalaId).HasColumnName("sala_id").HasMaxLength(32);
            round.Property(r => r.Number).HasColumnName("number");
            round.Property(r => r.ChainId).HasColumnName("chain_id");
            round.Property(r => r.ChainRoundIndex).HasColumnName("chain_round_index");
            round.Property(r => r.Commitment).HasColumnName("commitment").HasMaxLength(64);
            round.Property(r => r.State).HasColumnName("state").HasMaxLength(16);
            round.Property(r => r.BettingEndsAt).HasColumnName("betting_ends_at");
            round.Property(r => r.StartedAt).HasColumnName("started_at");
            round.Property(r => r.CrashedAt).HasColumnName("crashed_at");
            round.Property(r => r.CrashPoint).HasColumnName("crash_point").HasPrecision(12, 2);
            round.Property(r => r.RevealedSeed).HasColumnName("revealed_seed").HasMaxLength(64);
            round.Property(r => r.AnnulReason).HasColumnName("annul_reason").HasMaxLength(200);
            round.Property(r => r.AnnulledAt).HasColumnName("annulled_at");

            // One round per number in a room: two conductors can never persist the same round number.
            round.HasIndex(r => new { r.SalaId, r.Number }).IsUnique().HasDatabaseName("ux_rounds_sala_id_number");
        });

        modelBuilder.Entity<ChainRecord>(chain =>
        {
            chain.ToTable("chains");
            chain.HasKey(c => c.Id).HasName("pk_chains");
            chain.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            chain.Property(c => c.SalaId).HasColumnName("sala_id").HasMaxLength(32);
            chain.Property(c => c.TerminalHash).HasColumnName("terminal_hash").HasMaxLength(64);
            chain.Property(c => c.Salt).HasColumnName("salt").HasMaxLength(128);
            chain.Property(c => c.Length).HasColumnName("length");
            chain.Property(c => c.CreatedAt).HasColumnName("created_at");
        });
    }
}
