using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Rounds.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRoundsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rounds");

            migrationBuilder.CreateTable(
                name: "chains",
                schema: "rounds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sala_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    terminal_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    salt = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    length = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chains", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rounds",
                schema: "rounds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sala_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    number = table.Column<long>(type: "bigint", nullable: false),
                    chain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain_round_index = table.Column<int>(type: "integer", nullable: false),
                    commitment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    betting_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    crashed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    crash_point = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    revealed_seed = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    annul_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    annulled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rtp = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    cap = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    growth_k = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rounds", x => x.id);
                    table.CheckConstraint("ck_rounds_number", "number >= 1");
                    table.CheckConstraint("ck_rounds_outcome_after_crash", "(state = 'crashed') = (crash_point IS NOT NULL AND revealed_seed IS NOT NULL AND crashed_at IS NOT NULL)");
                    table.CheckConstraint("ck_rounds_parameters", "(rtp IS NULL) = (cap IS NULL) AND (rtp IS NULL) = (growth_k IS NULL)");
                    table.CheckConstraint("ck_rounds_state", "state IN ('betting', 'flying', 'crashed', 'annulled')");
                });

            migrationBuilder.CreateIndex(
                name: "ux_rounds_sala_id_number",
                schema: "rounds",
                table: "rounds",
                columns: new[] { "sala_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chains",
                schema: "rounds");

            migrationBuilder.DropTable(
                name: "rounds",
                schema: "rounds");
        }
    }
}
