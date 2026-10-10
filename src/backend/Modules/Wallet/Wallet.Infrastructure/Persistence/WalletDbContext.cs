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
    }
}
