using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>The DbContext of the Betting module: it maps only the schema <c>betting</c> (ADR-0010).</summary>
internal sealed class BettingDbContext(DbContextOptions<BettingDbContext> options) : DbContext(options)
{
    public const string Schema = "betting";

    /// <summary>One bet per <c>clientBetId</c> of a player (idempotency of <c>PlaceBet</c>, SPEC-0022 §4).</summary>
    public const string ClientBetIdIndex = "ux_bets_operator_player_client_bet_id";

    /// <summary>At most one placed bet (or reservation of one) per panel of a player in a round.</summary>
    public const string PlacedPanelIndex = "ux_bets_placed_panel";

    /// <summary>At most one queued bet (or reservation of one) per panel of a player (both for the next round).</summary>
    public const string QueuedPanelIndex = "ux_bets_queued_panel";

    /// <summary>The personal history of a player, most recent first (SPEC-0025 §6).</summary>
    public const string PlayerHistoryIndex = "ix_bets_player_history";

    public DbSet<BetRecord> Bets => Set<BetRecord>();

    /// <summary>The immutable summaries of the settled rounds (SPEC-0024).</summary>
    public DbSet<RoundSettlementRecord> RoundSettlements => Set<RoundSettlementRecord>();

    /// <summary>The rounds whose end Betting processed (SPEC-0024).</summary>
    public DbSet<RoundClosureRecord> RoundClosures => Set<RoundClosureRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<BetRecord>(bet =>
        {
            bet.ToTable("bets", table =>
            {
                table.HasCheckConstraint("ck_bets_status", "status IN ('pending', 'placed', 'active', 'queued', 'cancelled', 'cashed_out', 'lost', 'refunded')");
                table.HasCheckConstraint("ck_bets_accepted_status", "accepted_status IN ('placed', 'queued')");
                table.HasCheckConstraint("ck_bets_panel", "panel IN (1, 2)");
                table.HasCheckConstraint("ck_bets_amount", "amount > 0");

                // A bet has its round unless it is waiting for the next one.
                table.HasCheckConstraint("ck_bets_round", "(status = 'queued') = (round_id IS NULL) OR status IN ('cancelled', 'pending')");
                table.HasCheckConstraint("ck_bets_refund_status", "refund_status IS NULL OR refund_status IN ('refunded', 'pending')");
            });
            bet.HasKey(b => b.Id).HasName("pk_bets");
            bet.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
            bet.Property(b => b.OperatorId).HasColumnName("operator_id").HasMaxLength(64);
            bet.Property(b => b.PlayerId).HasColumnName("player_id").HasMaxLength(128);
            bet.Property(b => b.Currency).HasColumnName("currency").HasMaxLength(8);
            bet.Property(b => b.SalaId).HasColumnName("sala_id").HasMaxLength(32);
            bet.Property(b => b.ClientBetId).HasColumnName("client_bet_id");
            bet.Property(b => b.Panel).HasColumnName("panel");
            bet.Property(b => b.Amount).HasColumnName("amount").HasPrecision(18, 2);
            bet.Property(b => b.AutoCashOut).HasColumnName("auto_cash_out").HasPrecision(18, 8);
            bet.Property(b => b.Status).HasColumnName("status").HasMaxLength(16).IsConcurrencyToken();
            bet.Property(b => b.RoundId).HasColumnName("round_id");
            bet.Property(b => b.RoundNumber).HasColumnName("round_number");
            bet.Property(b => b.AcceptedStatus).HasColumnName("accepted_status").HasMaxLength(16);
            bet.Property(b => b.AcceptedRoundId).HasColumnName("accepted_round_id");
            bet.Property(b => b.AcceptedRoundNumber).HasColumnName("accepted_round_number");
            bet.Property(b => b.BalanceAfter).HasColumnName("balance_after").HasPrecision(18, 2);
            bet.Property(b => b.CreatedAt).HasColumnName("created_at");
            bet.Property(b => b.Refunded).HasColumnName("refunded").HasPrecision(18, 2);
            bet.Property(b => b.RefundStatus).HasColumnName("refund_status").HasMaxLength(16);
            bet.Property(b => b.RefundBalance).HasColumnName("refund_balance").HasPrecision(18, 2);

            bet.HasIndex(b => new { b.OperatorId, b.PlayerId, b.ClientBetId }).IsUnique().HasDatabaseName(ClientBetIdIndex);
            bet.HasIndex(b => new { b.OperatorId, b.PlayerId, b.Panel, b.RoundId })
                .IsUnique()
                .HasFilter("status = 'placed' OR (status = 'pending' AND accepted_status = 'placed')")
                .HasDatabaseName(PlacedPanelIndex);
            bet.HasIndex(b => new { b.OperatorId, b.PlayerId, b.Panel })
                .IsUnique()
                .HasFilter("status = 'queued' OR (status = 'pending' AND accepted_status = 'queued')")
                .HasDatabaseName(QueuedPanelIndex);

            // The bets a round event moves: the queued bets of a room and the placed bets of a round.
            bet.HasIndex(b => new { b.SalaId, b.Status }).HasDatabaseName("ix_bets_sala_id_status");
            bet.HasIndex(b => new { b.RoundId, b.Status }).HasDatabaseName("ix_bets_round_id_status");

            // The reservations the sweeper reverses (debit not confirmed in time).
            bet.HasIndex(b => b.CreatedAt).HasFilter("status = 'pending'").HasDatabaseName("ix_bets_pending_created_at");

            MapCashOut(bet);
            MapHistory(bet);
        });

        MapSettlement(modelBuilder);
    }

    /// <summary>The summaries of the settled rounds, unique per (round, operator, currency), and the rounds whose end was processed.</summary>
    private static void MapSettlement(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoundSettlementRecord>(settlement =>
        {
            settlement.ToTable("round_settlements", table => table.HasCheckConstraint("ck_round_settlements_ggr", "ggr = total_wagered - total_paid - total_refunded"));
            settlement.HasKey(s => new { s.RoundId, s.OperatorId, s.Currency }).HasName("pk_round_settlements");
            settlement.Property(s => s.RoundId).HasColumnName("round_id");
            settlement.Property(s => s.OperatorId).HasColumnName("operator_id").HasMaxLength(64);
            settlement.Property(s => s.Currency).HasColumnName("currency").HasMaxLength(8);
            settlement.Property(s => s.BetsCount).HasColumnName("bets_count");
            settlement.Property(s => s.TotalWagered).HasColumnName("total_wagered").HasPrecision(18, 2);
            settlement.Property(s => s.TotalPaid).HasColumnName("total_paid").HasPrecision(18, 2);
            settlement.Property(s => s.TotalRefunded).HasColumnName("total_refunded").HasPrecision(18, 2);
            settlement.Property(s => s.Ggr).HasColumnName("ggr").HasPrecision(18, 2);
            settlement.Property(s => s.SettledAt).HasColumnName("settled_at");
        });

        modelBuilder.Entity<RoundClosureRecord>(closure =>
        {
            closure.ToTable("round_closures", table => table.HasCheckConstraint("ck_round_closures_outcome", "outcome IN ('crashed', 'annulled')"));
            closure.HasKey(c => c.RoundId).HasName("pk_round_closures");
            closure.Property(c => c.RoundId).HasColumnName("round_id").ValueGeneratedNever();
            closure.Property(c => c.Outcome).HasColumnName("outcome").HasMaxLength(16);
            closure.Property(c => c.ClosedAt).HasColumnName("closed_at");
        });
    }

    /// <summary>The personal history of a player (SPEC-0025): by acceptance instant and id, descending, paged by keyset.</summary>
    private static void MapHistory(EntityTypeBuilder<BetRecord> bet) =>
        bet.HasIndex(b => new { b.OperatorId, b.PlayerId, b.CreatedAt, b.Id })
            .IsDescending(false, false, true, true)
            .HasDatabaseName(PlayerHistoryIndex);

    /// <summary>The cash-out of a bet (SPEC-0023): its multiplier, payout and credit, and a manual request waiting for the leader.</summary>
    private static void MapCashOut(EntityTypeBuilder<BetRecord> bet)
    {
        bet.ToTable(table =>
        {
            // A cashed-out bet has its multiplier, payout and credit; no other bet has them.
            table.HasCheckConstraint("ck_bets_credit_status", "credit_status IS NULL OR credit_status IN ('credited', 'pending')");
            table.HasCheckConstraint(
                "ck_bets_cash_out",
                "(status = 'cashed_out') = (cash_out_multiplier IS NOT NULL AND payout IS NOT NULL AND credit_status IS NOT NULL)");
        });
        bet.Property(b => b.CashOutMultiplier).HasColumnName("cash_out_multiplier").HasPrecision(18, 8);
        bet.Property(b => b.Payout).HasColumnName("payout").HasPrecision(18, 2);
        bet.Property(b => b.CashOutAuto).HasColumnName("cash_out_auto");
        bet.Property(b => b.CashedOutAt).HasColumnName("cashed_out_at");
        bet.Property(b => b.CreditStatus).HasColumnName("credit_status").HasMaxLength(16);
        bet.Property(b => b.CreditBalance).HasColumnName("credit_balance").HasPrecision(18, 2);
        // A concurrency token: of concurrent pending requests the first reception instant stays (review G4, m1).
        bet.Property(b => b.CashOutRequestedAt).HasColumnName("cash_out_requested_at").IsConcurrencyToken();

        // The credits of cash-outs not confirmed yet (betting.credits_pending, SPEC-0023 §8).
        bet.HasIndex(b => b.CashedOutAt).HasFilter("credit_status = 'pending'").HasDatabaseName("ix_bets_credit_pending");
    }
}
