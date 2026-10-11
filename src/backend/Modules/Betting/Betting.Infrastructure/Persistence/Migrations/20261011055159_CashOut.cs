using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Betting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CashOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets");

            migrationBuilder.AddColumn<bool>(
                name: "cash_out_auto",
                schema: "betting",
                table: "bets",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cash_out_multiplier",
                schema: "betting",
                table: "bets",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cash_out_requested_at",
                schema: "betting",
                table: "bets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cashed_out_at",
                schema: "betting",
                table: "bets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_balance",
                schema: "betting",
                table: "bets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credit_status",
                schema: "betting",
                table: "bets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "payout",
                schema: "betting",
                table: "bets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_bets_credit_pending",
                schema: "betting",
                table: "bets",
                column: "cashed_out_at",
                filter: "credit_status = 'pending'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_cash_out",
                schema: "betting",
                table: "bets",
                sql: "(status = 'cashed_out') = (cash_out_multiplier IS NOT NULL AND payout IS NOT NULL AND credit_status IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_credit_status",
                schema: "betting",
                table: "bets",
                sql: "credit_status IS NULL OR credit_status IN ('credited', 'pending')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets",
                sql: "status IN ('pending', 'placed', 'active', 'queued', 'cancelled', 'cashed_out')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bets_credit_pending",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_cash_out",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_credit_status",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropColumn(
                name: "cash_out_auto",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropColumn(
                name: "cash_out_multiplier",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropColumn(
                name: "cash_out_requested_at",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropColumn(
                name: "cashed_out_at",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropColumn(
                name: "credit_balance",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropColumn(
                name: "credit_status",
                schema: "betting",
                table: "bets");

            migrationBuilder.DropColumn(
                name: "payout",
                schema: "betting",
                table: "bets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bets_status",
                schema: "betting",
                table: "bets",
                sql: "status IN ('pending', 'placed', 'active', 'queued', 'cancelled')");
        }
    }
}
