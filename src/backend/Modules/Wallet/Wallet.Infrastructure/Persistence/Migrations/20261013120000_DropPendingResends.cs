using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Wallet.Infrastructure.Persistence.Migrations
{
    /// <summary>SPEC-0046 (G4): the resend of credits and rollbacks is a scheduled Wolverine message (ADR-0002/0018); the own queue goes away.</summary>
    public partial class DropPendingResends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_resends",
                schema: "wallet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_resends",
                schema: "wallet",
                columns: table => new
                {
                    transaction_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    operator_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    player_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    round_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_transaction_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pending_resends", x => x.transaction_id);
                    table.CheckConstraint("ck_pending_resends_kind", "kind IN ('Credit', 'Rollback')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_pending_resends_next_attempt_at",
                schema: "wallet",
                table: "pending_resends",
                column: "next_attempt_at");
        }
    }
}
