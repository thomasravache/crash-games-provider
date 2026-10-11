using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Betting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendingReservationsAndRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_round",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets");

            migrationBuilder.AddColumn<decimal>(
                name: "refund_balance",
                schema: "betting",
                table: "bets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refund_status",
                schema: "betting",
                table: "bets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "refunded",
                schema: "betting",
                table: "bets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_bets_pending_created_at",
                schema: "betting",
                table: "bets",
                column: "created_at",
                filter: "status = 'pending'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_refund_status",
                schema: "betting",
                table: "bets",
                sql: "refund_status IS NULL OR refund_status IN ('refunded', 'pending')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_round",
                schema: "betting",
                table: "bets",
                sql: "(status = 'queued') = (round_id IS NULL) OR status IN ('cancelled', 'pending')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets",
                sql: "status IN ('pending', 'placed', 'active', 'queued', 'cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bets_pending_created_at",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_refund_status",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_round",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropColumn(
                name: "refund_balance",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropColumn(
                name: "refund_status",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropColumn(
                name: "refunded",
                schema: "betting",
                table: "bets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_round",
                schema: "betting",
                table: "bets",
                sql: "(status = 'queued') = (round_id IS NULL) OR status = 'cancelled'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets",
                sql: "status IN ('placed', 'active', 'queued', 'cancelled')");
        }
    }
}
