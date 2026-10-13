using Microsoft.EntityFrameworkCore;

namespace Apogeu.Wallet.Infrastructure.Persistence;

/// <summary>The DbContext of the Wallet module: it maps only the schema <c>wallet</c> (ADR-0010).</summary>
internal sealed class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public const string Schema = WalletLedgerTables.Schema;

    /// <summary>Precision of the amounts: the two decimal places of the contract money (ADR-0008).</summary>
    private const int AmountPrecision = 18;
    private const int AmountScale = 2;

    public DbSet<LedgerEntryRecord> Entries => Set<LedgerEntryRecord>();

    public DbSet<LedgerPostingRecord> Postings => Set<LedgerPostingRecord>();

    public DbSet<ResendChainRecord> ResendChains => Set<ResendChainRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<LedgerEntryRecord>(entity =>
        {
            entity.ToTable(WalletLedgerTables.Entries, table =>
                table.HasCheckConstraint("ck_ledger_entries_kind", "kind IN ('Debit', 'Credit', 'Rollback', 'Recharge')"));
            entity.HasKey(e => e.TransactionId).HasName("pk_ledger_entries");
            entity.Property(e => e.TransactionId).HasColumnName(WalletLedgerTables.TransactionIdColumn).HasMaxLength(128);
            entity.Property(e => e.Kind).HasColumnName("kind").HasMaxLength(16);
            entity.Property(e => e.OperatorId).HasColumnName("operator_id").HasMaxLength(64);
            entity.Property(e => e.PlayerId).HasColumnName("player_id").HasMaxLength(128);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3);
            entity.Property(e => e.RoundId).HasColumnName("round_id");
            entity.Property(e => e.BetId).HasColumnName("bet_id");
            entity.Property(e => e.OriginalTransactionId).HasColumnName("original_transaction_id").HasMaxLength(128);
            entity.Property(e => e.BalanceAfter).HasColumnName("balance_after").HasPrecision(AmountPrecision, AmountScale);
            entity.Property(e => e.RecordedAt).HasColumnName("recorded_at");
            // A debit is refunded at most once, whatever the id of the rollback (SPEC-0021 UT-06).
            entity.HasIndex(e => e.OriginalTransactionId)
                .IsUnique()
                .HasFilter("kind = 'Rollback'")
                .HasDatabaseName("ux_ledger_entries_rollback_original");
            entity.HasMany(e => e.Postings).WithOne().HasForeignKey(p => p.TransactionId)
                .HasConstraintName("fk_ledger_postings_ledger_entries_transaction_id")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LedgerPostingRecord>(entity =>
        {
            entity.ToTable(WalletLedgerTables.Postings);
            entity.HasKey(p => new { p.TransactionId, p.Line }).HasName("pk_ledger_postings");
            entity.Property(p => p.TransactionId).HasColumnName(WalletLedgerTables.TransactionIdColumn).HasMaxLength(128);
            entity.Property(p => p.Line).HasColumnName("line");
            entity.Property(p => p.OperatorId).HasColumnName("operator_id").HasMaxLength(64);
            entity.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3);
            entity.Property(p => p.PlayerId).HasColumnName("player_id").HasMaxLength(128);
            entity.Property(p => p.Amount).HasColumnName(WalletLedgerTables.AmountColumn).HasPrecision(AmountPrecision, AmountScale);

            // The balance of an account is the sum of its postings, read on every operation.
            entity.HasIndex(p => new { p.OperatorId, p.Currency, p.PlayerId })
                .HasDatabaseName("ix_ledger_postings_account")
                .IncludeProperties(p => p.Amount);
        });

        // One resend chain per transaction of the seamless wallet (SPEC-0046 IT-08): mutable, unlike the ledger.
        modelBuilder.Entity<ResendChainRecord>(entity =>
        {
            entity.ToTable(WalletLedgerTables.ResendChains, table =>
                table.HasCheckConstraint("ck_resend_chains_kind", "kind IN ('Credit', 'Rollback')"));
            entity.HasKey(c => c.TransactionId).HasName("pk_resend_chains");
            entity.Property(c => c.TransactionId).HasColumnName(WalletLedgerTables.TransactionIdColumn).HasMaxLength(128);
            entity.Property(c => c.Kind).HasColumnName("kind").HasMaxLength(16);
            entity.Property(c => c.OperatorId).HasColumnName("operator_id").HasMaxLength(64);
            entity.Property(c => c.CreatedAt).HasColumnName("created_at");
        });
    }
}
