using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Operators.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorsCatalogAndLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "games",
                schema: "operators",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    client_app = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    theme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rtp = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    cap = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    growth_k = table.Column<double>(type: "double precision", nullable: false),
                    betting_seconds = table.Column<int>(type: "integer", nullable: false),
                    pause_seconds = table.Column<int>(type: "integer", nullable: false),
                    currencies = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.code);
                    table.CheckConstraint("ck_games_cap", "cap > 1");
                    table.CheckConstraint("ck_games_growth_k", "growth_k > 0");
                    table.CheckConstraint("ck_games_phases", "betting_seconds > 0 AND pause_seconds >= 0");
                    table.CheckConstraint("ck_games_rtp", "rtp > 0 AND rtp < 1");
                });

            migrationBuilder.CreateTable(
                name: "operator_limits",
                schema: "operators",
                columns: table => new
                {
                    operator_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    min_bet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    max_bet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    max_win = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operator_limits", x => new { x.operator_id, x.currency });
                    table.CheckConstraint("ck_operator_limits_amounts", "min_bet > 0 AND max_bet >= min_bet AND max_win >= max_bet");
                    table.CheckConstraint("ck_operator_limits_currency", "currency ~ '^[A-Z]{3}$'");
                    table.ForeignKey(
                        name: "fk_operator_limits_operators_operator_id",
                        column: x => x.operator_id,
                        principalSchema: "operators",
                        principalTable: "operators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operator_games",
                schema: "operators",
                columns: table => new
                {
                    operator_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    game_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operator_games", x => new { x.operator_id, x.game_code });
                    table.ForeignKey(
                        name: "fk_operator_games_games_game_code",
                        column: x => x.game_code,
                        principalSchema: "operators",
                        principalTable: "games",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_operator_games_operators_operator_id",
                        column: x => x.operator_id,
                        principalSchema: "operators",
                        principalTable: "operators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_operator_games_game_code",
                schema: "operators",
                table: "operator_games",
                column: "game_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operator_games",
                schema: "operators");

            migrationBuilder.DropTable(
                name: "operator_limits",
                schema: "operators");

            migrationBuilder.DropTable(
                name: "games",
                schema: "operators");
        }
    }
}
