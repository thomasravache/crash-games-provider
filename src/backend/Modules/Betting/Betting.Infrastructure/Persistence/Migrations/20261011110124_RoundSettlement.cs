using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Betting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoundSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets");

            migrationBuilder.CreateTable(
                name: "round_closures",
                schema: "betting",
                columns: table => new
                {
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_round_closures", x => x.round_id);
                    table.CheckConstraint("ck_round_closures_outcome", "outcome IN ('crashed', 'annulled')");
                });

            migrationBuilder.CreateTable(
                name: "round_settlements",
                schema: "betting",
                columns: table => new
                {
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    bets_count = table.Column<int>(type: "integer", nullable: false),
                    total_wagered = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_refunded = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ggr = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_round_settlements", x => new { x.round_id, x.operator_id, x.currency });
                    table.CheckConstraint("ck_round_settlements_ggr", "ggr = total_wagered - total_paid - total_refunded");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets",
                sql: "status IN ('pending', 'placed', 'active', 'queued', 'cancelled', 'cashed_out', 'lost', 'refunded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "round_closures",
                schema: "betting");

            migrationBuilder.DropTable(
                name: "round_settlements",
                schema: "betting");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets",
                sql: "status IN ('pending', 'placed', 'active', 'queued', 'cancelled', 'cashed_out')");
        }
    }
}
