using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Apogeu.Fairness.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFairnessSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fairness");

            migrationBuilder.CreateTable(
                name: "chains",
                schema: "fairness",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sala_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    length = table.Column<int>(type: "integer", nullable: false),
                    issued_rounds = table.Column<int>(type: "integer", nullable: false),
                    terminal_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    salt = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chains", x => x.id);
                    table.CheckConstraint("ck_chains_issued_rounds", "issued_rounds >= 0 AND issued_rounds <= length");
                });

            migrationBuilder.CreateTable(
                name: "checkpoints",
                schema: "fairness",
                columns: table => new
                {
                    chain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_index = table.Column<int>(type: "integer", nullable: false),
                    protected_link = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_checkpoints", x => new { x.chain_id, x.link_index });
                    table.ForeignKey(
                        name: "fk_checkpoints_chains_chain_id",
                        column: x => x.chain_id,
                        principalSchema: "fairness",
                        principalTable: "chains",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chains_sala_id_sequence",
                schema: "fairness",
                table: "chains",
                columns: new[] { "sala_id", "sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkpoints",
                schema: "fairness");

            migrationBuilder.DropTable(
                name: "chains",
                schema: "fairness");
        }
    }
}
