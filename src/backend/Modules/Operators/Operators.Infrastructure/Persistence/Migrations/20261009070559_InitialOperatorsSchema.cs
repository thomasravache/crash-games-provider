using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Operators.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOperatorsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "operators");

            migrationBuilder.CreateTable(
                name: "operators",
                schema: "operators",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sala_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operators", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "launch_grants",
                schema: "operators",
                columns: table => new
                {
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operator_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sala_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    player_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    nickname = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    redeemed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_launch_grants", x => x.token_hash);
                    table.ForeignKey(
                        name: "fk_launch_grants_operators_operator_id",
                        column: x => x.operator_id,
                        principalSchema: "operators",
                        principalTable: "operators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_launch_grants_expires_at",
                schema: "operators",
                table: "launch_grants",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_launch_grants_operator_id",
                schema: "operators",
                table: "launch_grants",
                column: "operator_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "launch_grants",
                schema: "operators");

            migrationBuilder.DropTable(
                name: "operators",
                schema: "operators");
        }
    }
}
