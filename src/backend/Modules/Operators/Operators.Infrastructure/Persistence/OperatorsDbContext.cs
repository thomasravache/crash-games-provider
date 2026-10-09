using Microsoft.EntityFrameworkCore;

namespace Apogeu.Operators.Infrastructure.Persistence;

/// <summary>The DbContext of the Operators module: it maps only the schema <c>operators</c> (ADR-0010).</summary>
internal sealed class OperatorsDbContext(DbContextOptions<OperatorsDbContext> options) : DbContext(options)
{
    public const string Schema = "operators";

    public DbSet<OperatorRecord> Operators => Set<OperatorRecord>();

    public DbSet<LaunchGrantRecord> LaunchGrants => Set<LaunchGrantRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<OperatorRecord>(entity =>
        {
            entity.ToTable("operators");
            entity.HasKey(o => o.Id).HasName("pk_operators");
            entity.Property(o => o.Id).HasColumnName("id").HasMaxLength(64);
            entity.Property(o => o.Name).HasColumnName("name").HasMaxLength(128);
            entity.Property(o => o.SalaId).HasColumnName("sala_id").HasMaxLength(64);
        });

        modelBuilder.Entity<LaunchGrantRecord>(entity =>
        {
            entity.ToTable("launch_grants");
            entity.HasKey(g => g.TokenHash).HasName("pk_launch_grants");
            entity.Property(g => g.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
            entity.Property(g => g.OperatorId).HasColumnName("operator_id").HasMaxLength(64);
            entity.Property(g => g.SalaId).HasColumnName("sala_id").HasMaxLength(64);
            entity.Property(g => g.PlayerId).HasColumnName("player_id").HasMaxLength(128);
            entity.Property(g => g.Nickname).HasColumnName("nickname").HasMaxLength(32);
            entity.Property(g => g.Currency).HasColumnName("currency").HasMaxLength(3);
            entity.Property(g => g.Mode).HasColumnName("mode").HasMaxLength(16);
            entity.Property(g => g.Language).HasColumnName("language").HasMaxLength(16);
            entity.Property(g => g.IssuedAt).HasColumnName("issued_at");
            entity.Property(g => g.ExpiresAt).HasColumnName("expires_at");
            entity.Property(g => g.RedeemedAt).HasColumnName("redeemed_at");
            entity.HasIndex(g => g.ExpiresAt).HasDatabaseName("ix_launch_grants_expires_at");
            entity.HasIndex(g => g.OperatorId).HasDatabaseName("ix_launch_grants_operator_id");
            entity.HasOne<OperatorRecord>().WithMany().HasForeignKey(g => g.OperatorId)
                .HasConstraintName("fk_launch_grants_operators_operator_id")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
