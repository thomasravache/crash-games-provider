using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Wallet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeamlessResendChains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resend_chains",
                schema: "wallet",
                columns: table => new
                {
                    transaction_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    operator_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resend_chains", x => x.transaction_id);
                    table.CheckConstraint("ck_resend_chains_kind", "kind IN ('Credit', 'Rollback')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resend_chains",
                schema: "wallet");
        }
    }
}
