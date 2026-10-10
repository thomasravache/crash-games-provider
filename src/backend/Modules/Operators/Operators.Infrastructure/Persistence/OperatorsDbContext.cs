using Microsoft.EntityFrameworkCore;

namespace Apogeu.Operators.Infrastructure.Persistence;

/// <summary>The DbContext of the Operators module: it maps only the schema <c>operators</c> (ADR-0010).</summary>
internal sealed class OperatorsDbContext(DbContextOptions<OperatorsDbContext> options) : DbContext(options)
{
    public const string Schema = "operators";

    public DbSet<OperatorRecord> Operators => Set<OperatorRecord>();

    public DbSet<LaunchGrantRecord> LaunchGrants => Set<LaunchGrantRecord>();

    public DbSet<GameRecord> Games => Set<GameRecord>();

    public DbSet<OperatorGameRecord> OperatorGames => Set<OperatorGameRecord>();

    public DbSet<OperatorLimitsRecord> OperatorLimits => Set<OperatorLimitsRecord>();

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

        MapCatalog(modelBuilder);
    }

    /// <summary>The game catalog, the games enabled per operator and the limits per operator and currency (SPEC-0020).</summary>
    private static void MapCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameRecord>(entity =>
        {
            entity.ToTable("games", table =>
            {
                table.HasCheckConstraint("ck_games_rtp", "rtp > 0 AND rtp < 1");
                table.HasCheckConstraint("ck_games_cap", "cap > 1");
                table.HasCheckConstraint("ck_games_growth_k", "growth_k > 0");
                table.HasCheckConstraint("ck_games_phases", "betting_seconds > 0 AND pause_seconds >= 0");
            });
            entity.HasKey(g => g.Code).HasName("pk_games");
            entity.Property(g => g.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(g => g.Name).HasColumnName("name").HasMaxLength(128);
            entity.Property(g => g.ClientApp).HasColumnName("client_app").HasMaxLength(64);
            entity.Property(g => g.Theme).HasColumnName("theme").HasMaxLength(64);
            entity.Property(g => g.Rtp).HasColumnName("rtp").HasPrecision(5, 4);
            entity.Property(g => g.Cap).HasColumnName("cap").HasPrecision(12, 2);
            entity.Property(g => g.GrowthK).HasColumnName("growth_k");
            entity.Property(g => g.BettingSeconds).HasColumnName("betting_seconds");
            entity.Property(g => g.PauseSeconds).HasColumnName("pause_seconds");
            entity.Property(g => g.Currencies).HasColumnName("currencies");
        });

        modelBuilder.Entity<OperatorGameRecord>(entity =>
        {
            entity.ToTable("operator_games");
            entity.HasKey(e => new { e.OperatorId, e.GameCode }).HasName("pk_operator_games");
            entity.Property(e => e.OperatorId).HasColumnName("operator_id").HasMaxLength(64);
            entity.Property(e => e.GameCode).HasColumnName("game_code").HasMaxLength(64);
            entity.Property(e => e.Enabled).HasColumnName("enabled");
            entity.HasIndex(e => e.GameCode).HasDatabaseName("ix_operator_games_game_code");
            entity.HasOne<OperatorRecord>().WithMany().HasForeignKey(e => e.OperatorId)
                .HasConstraintName("fk_operator_games_operators_operator_id")
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GameRecord>().WithMany().HasForeignKey(e => e.GameCode)
                .HasConstraintName("fk_operator_games_games_game_code")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OperatorLimitsRecord>(entity =>
        {
            entity.ToTable("operator_limits", table =>
            {
                table.HasCheckConstraint("ck_operator_limits_currency", "currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("ck_operator_limits_amounts", "min_bet > 0 AND max_bet >= min_bet AND max_win >= max_bet");
            });
            entity.HasKey(l => new { l.OperatorId, l.Currency }).HasName("pk_operator_limits");
            entity.Property(l => l.OperatorId).HasColumnName("operator_id").HasMaxLength(64);
            entity.Property(l => l.Currency).HasColumnName("currency").HasMaxLength(3);
            entity.Property(l => l.MinBet).HasColumnName("min_bet").HasPrecision(18, 2);
            entity.Property(l => l.MaxBet).HasColumnName("max_bet").HasPrecision(18, 2);
            entity.Property(l => l.MaxWin).HasColumnName("max_win").HasPrecision(18, 2);
            entity.HasOne<OperatorRecord>().WithMany().HasForeignKey(l => l.OperatorId)
                .HasConstraintName("fk_operator_limits_operators_operator_id")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
