using Microsoft.EntityFrameworkCore;

namespace Apogeu.Fairness.Infrastructure.Persistence;

/// <summary>The DbContext of the Fairness module: it maps only the schema <c>fairness</c> (ADR-0010).</summary>
internal sealed class FairnessDbContext(DbContextOptions<FairnessDbContext> options) : DbContext(options)
{
    public const string Schema = "fairness";

    public DbSet<ChainRecord> Chains => Set<ChainRecord>();

    public DbSet<CheckpointRecord> Checkpoints => Set<CheckpointRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ChainRecord>(chain =>
        {
            chain.ToTable("chains", table => table.HasCheckConstraint(
                "ck_chains_issued_rounds",
                "issued_rounds >= 0 AND issued_rounds <= length"));
            chain.HasKey(c => c.Id).HasName("pk_chains");
            chain.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            chain.Property(c => c.SalaId).HasColumnName("sala_id");
            chain.Property(c => c.Sequence).HasColumnName("sequence").UseIdentityAlwaysColumn();
            chain.Property(c => c.Length).HasColumnName("length");
            chain.Property(c => c.IssuedRounds).HasColumnName("issued_rounds");
            chain.Property(c => c.TerminalHash).HasColumnName("terminal_hash");
            chain.Property(c => c.Salt).HasColumnName("salt");
            chain.HasIndex(c => new { c.SalaId, c.Sequence }).HasDatabaseName("ix_chains_sala_id_sequence");
        });

        modelBuilder.Entity<CheckpointRecord>(checkpoint =>
        {
            checkpoint.ToTable("checkpoints");
            checkpoint.HasKey(c => new { c.ChainId, c.LinkIndex }).HasName("pk_checkpoints");
            checkpoint.Property(c => c.ChainId).HasColumnName("chain_id");
            checkpoint.Property(c => c.LinkIndex).HasColumnName("link_index");
            checkpoint.Property(c => c.ProtectedLink).HasColumnName("protected_link");
            checkpoint.HasOne<ChainRecord>().WithMany().HasForeignKey(c => c.ChainId)
                .HasConstraintName("fk_checkpoints_chains_chain_id")
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
