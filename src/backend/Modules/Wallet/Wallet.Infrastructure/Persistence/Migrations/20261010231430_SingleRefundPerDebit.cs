using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Wallet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SingleRefundPerDebit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_ledger_entries_rollback_original",
                schema: "wallet",
                table: "ledger_entries",
                column: "original_transaction_id",
                unique: true,
                filter: "kind = 'Rollback'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_ledger_entries_rollback_original",
                schema: "wallet",
                table: "ledger_entries");
        }
    }
}
