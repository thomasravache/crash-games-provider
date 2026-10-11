using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Betting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendingReservationsHoldPanels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_bets_placed_panel",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropIndex(
                name: "ux_bets_queued_panel",
                schema: "betting",
                table: "bets");

            migrationBuilder.CreateIndex(
                name: "ux_bets_placed_panel",
                schema: "betting",
                table: "bets",
                columns: new[] { "operator_id", "player_id", "panel", "round_id" },
                unique: true,
                filter: "status = 'placed' OR (status = 'pending' AND accepted_status = 'placed')");

            migrationBuilder.CreateIndex(
                name: "ux_bets_queued_panel",
                schema: "betting",
                table: "bets",
                columns: new[] { "operator_id", "player_id", "panel" },
                unique: true,
                filter: "status = 'queued' OR (status = 'pending' AND accepted_status = 'queued')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_bets_placed_panel",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropIndex(
                name: "ux_bets_queued_panel",
                schema: "betting",
                table: "bets");

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
    }
}
