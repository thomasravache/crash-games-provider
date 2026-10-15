using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Betting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlayerHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_bets_player_history",
                schema: "betting",
                table: "bets",
                columns: new[] { "operator_id", "player_id", "created_at", "id" },
                descending: new[] { false, false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bets_player_history",
                schema: "betting",
                table: "bets");
        }
    }
}
