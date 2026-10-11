using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Wallet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWalletSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "wallet");

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                schema: "wallet",
                columns: table => new
                {
                    transaction_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    operator_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    player_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_transaction_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_entries", x => x.transaction_id);
                    table.CheckConstraint("ck_ledger_entries_kind", "kind IN ('Debit', 'Credit', 'Rollback', 'Recharge')");
                });

            migrationBuilder.CreateTable(
                name: "ledger_postings",
                schema: "wallet",
                columns: table => new
                {
                    transaction_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    line = table.Column<short>(type: "smallint", nullable: false),
                    operator_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    player_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_postings", x => new { x.transaction_id, x.line });
                    table.ForeignKey(
                        name: "fk_ledger_postings_ledger_entries_transaction_id",
                        column: x => x.transaction_id,
                        principalSchema: "wallet",
                        principalTable: "ledger_entries",
                        principalColumn: "transaction_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_postings_account",
                schema: "wallet",
                table: "ledger_postings",
                columns: new[] { "operator_id", "currency", "player_id" })
                .Annotation("Npgsql:IndexInclude", new[] { "amount" });

            // SPEC-0021 §8, ADR-0007: the ledger is insert-only. Privileges do not bind the owner of the tables nor a
            // superuser, so a trigger refuses every UPDATE, DELETE and TRUNCATE whoever runs it.
            migrationBuilder.Sql("""
                CREATE FUNCTION wallet.refuse_ledger_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'The wallet ledger is insert-only: % on %.% is refused.', TG_OP, TG_TABLE_SCHEMA, TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;
                """);
            foreach (var table in new[] { "ledger_entries", "ledger_postings" })
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER {table}_insert_only BEFORE UPDATE OR DELETE ON wallet.{table}
                        FOR EACH ROW EXECUTE FUNCTION wallet.refuse_ledger_change();
                    CREATE TRIGGER {table}_no_truncate BEFORE TRUNCATE ON wallet.{table}
                        FOR EACH STATEMENT EXECUTE FUNCTION wallet.refuse_ledger_change();
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "ledger_entries", "ledger_postings" })
            {
                migrationBuilder.Sql($"""
                    DROP TRIGGER {table}_no_truncate ON wallet.{table};
                    DROP TRIGGER {table}_insert_only ON wallet.{table};
                    """);
            }

            migrationBuilder.Sql("DROP FUNCTION wallet.refuse_ledger_change();");

            migrationBuilder.DropTable(
                name: "ledger_postings",
                schema: "wallet");

            migrationBuilder.DropTable(
                name: "ledger_entries",
                schema: "wallet");
        }
    }
}
