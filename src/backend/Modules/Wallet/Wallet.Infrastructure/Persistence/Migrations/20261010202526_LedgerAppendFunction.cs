using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apogeu.Wallet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LedgerAppendFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SPEC-0021 §5 (IT-05, IT-07): the append of an entry in one round trip. The advisory lock serializes the appends of
            // one player account; each later statement of a VOLATILE function takes a fresh READ COMMITTED snapshot, so it sees
            // what the previous holder of the lock committed. Returns 'appended', 'duplicate' (the transaction id exists) or
            // 'stale' (the player's sum is not the expected one); nothing is written unless 'appended'.
            migrationBuilder.Sql("""
                CREATE FUNCTION wallet.try_append_entry(
                    p_transaction_id text,
                    p_kind text,
                    p_operator_id text,
                    p_player_id text,
                    p_currency text,
                    p_round_id uuid,
                    p_bet_id uuid,
                    p_original_transaction_id text,
                    p_balance_after numeric,
                    p_recorded_at timestamptz,
                    p_expected_player_sum numeric,
                    p_posting_players text[],
                    p_posting_amounts numeric[])
                RETURNS text
                LANGUAGE plpgsql VOLATILE AS $$
                BEGIN
                    PERFORM pg_advisory_xact_lock(hashtextextended(concat_ws(E'\x1f', 'wallet', p_operator_id, p_currency, p_player_id), 0));

                    IF EXISTS (SELECT 1 FROM wallet.ledger_entries WHERE transaction_id = p_transaction_id) THEN
                        RETURN 'duplicate';
                    END IF;

                    IF (SELECT COALESCE(sum(amount), 0) FROM wallet.ledger_postings
                        WHERE operator_id = p_operator_id AND currency = p_currency AND player_id = p_player_id) <> p_expected_player_sum THEN
                        RETURN 'stale';
                    END IF;

                    INSERT INTO wallet.ledger_entries (transaction_id, kind, operator_id, player_id, currency, round_id, bet_id,
                        original_transaction_id, balance_after, recorded_at)
                    VALUES (p_transaction_id, p_kind, p_operator_id, p_player_id, p_currency, p_round_id, p_bet_id,
                        p_original_transaction_id, p_balance_after, p_recorded_at);

                    INSERT INTO wallet.ledger_postings (transaction_id, line, operator_id, currency, player_id, amount)
                    SELECT p_transaction_id, posting.line::smallint, p_operator_id, p_currency, posting.player_id, posting.amount
                    FROM unnest(p_posting_players, p_posting_amounts) WITH ORDINALITY AS posting(player_id, amount, line);

                    RETURN 'appended';
                EXCEPTION
                    -- The same transaction id recorded meanwhile for another account (not serialized by this lock).
                    WHEN unique_violation THEN
                        RETURN 'duplicate';
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION wallet.try_append_entry(text, text, text, text, text, uuid, uuid, text, numeric, timestamptz, numeric, text[], numeric[]);");
        }
    }
}
