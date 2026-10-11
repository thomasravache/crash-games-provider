using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Betting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBettingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "betting");

            migrationBuilder.CreateTable(
                name: "bets",
                schema: "betting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    player_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    sala_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    client_bet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    panel = table.Column<short>(type: "smallint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    auto_cash_out = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: true),
                    round_number = table.Column<long>(type: "bigint", nullable: true),
                    accepted_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    accepted_round_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accepted_round_number = table.Column<long>(type: "bigint", nullable: true),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bets", x => x.id);
                    table.CheckConstraint("ck_bets_accepted_status", "accepted_status IN ('placed', 'queued')");
                    table.CheckConstraint("ck_bets_amount", "amount > 0");
                    table.CheckConstraint("ck_bets_panel", "panel IN (1, 2)");
                    table.CheckConstraint("ck_bets_round", "(status = 'queued') = (round_id IS NULL) OR status = 'cancelled'");
                    table.CheckConstraint("ck_bets_status", "status IN ('placed', 'active', 'queued', 'cancelled')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_bets_round_id_status",
                schema: "betting",
                table: "bets",
                columns: new[] { "round_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_bets_sala_id_status",
                schema: "betting",
                table: "bets",
                columns: new[] { "sala_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_bets_operator_player_client_bet_id",
                schema: "betting",
                table: "bets",
                columns: new[] { "operator_id", "player_id", "client_bet_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_bets_placed_panel",
                schema: "betting",
                table: "bets",
                columns: new[] { "operator_id", "player_id", "panel", "round_id" },
                unique: true,
                filter: "status = 'placed'");

            migrationBuilder.CreateIndex(
                name: "ux_bets_queued_panel",
                schema: "betting",
                table: "bets",
                columns: new[] { "operator_id", "player_id", "panel" },
                unique: true,
                filter: "status = 'queued'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bets",
                schema: "betting");
        }
    }
}
