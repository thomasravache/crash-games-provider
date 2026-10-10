using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Tests.Support;
using Npgsql;
using Xunit;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Integration;

/// <summary>
/// IT-06: the ledger is insert-only (SPEC-0021 §8, ADR-0007): an UPDATE, DELETE or TRUNCATE of entries or postings, run with the
/// module's own connection, is refused by the database (insert-only triggers), even for the owner of the
/// tables and the superuser of the tests.
/// </summary>
[Collection(WalletPostgres.Name)]
public sealed class LedgerImmutabilityTests(PostgreSqlContainerFixture postgres)
{
    [Fact(DisplayName = "Dado um lançamento gravado, quando se tenta UPDATE/DELETE/TRUNCATE direto no banco com a conexão do módulo, então o banco recusa e nada muda")]
    [Trait("Spec", "SPEC-0021:IT-06")]
    [Trait("Category", "Integration")]
    public async Task GivenRecordedEntryWhenUpdatingOrDeletingWithModuleConnectionThenDatabaseRefuses()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct);
        var player = NewPlayer();
        var bet = Guid.NewGuid();
        AssertOk(await module.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 10.00m, bet), ct)), 2_990.00m);

        // The connection of the module is the superuser of the container: privileges alone would not stop it.
        Assert.True(await LedgerRows.ScalarAsync<bool>(module.ConnectionString, "SELECT rolsuper FROM pg_roles WHERE rolname = current_user", parameter: null, ct));

        var statements = new[]
        {
            $"UPDATE {LedgerRows.EntriesTable} SET {LedgerRows.TransactionIdColumn} = {LedgerRows.TransactionIdColumn}",
            $"DELETE FROM {LedgerRows.EntriesTable}",
            $"UPDATE {LedgerRows.PostingsTable} SET {LedgerRows.TransactionIdColumn} = {LedgerRows.TransactionIdColumn}",
            $"DELETE FROM {LedgerRows.PostingsTable}",

            // TRUNCATE skips row triggers: postings alone, then both tables together (so no foreign key refuses it first).
            $"TRUNCATE {LedgerRows.PostingsTable}",
            $"TRUNCATE {LedgerRows.EntriesTable}, {LedgerRows.PostingsTable}",
        };
        foreach (var sql in statements)
        {
            var error = await Record.ExceptionAsync(() => LedgerRows.ExecuteAsync(module.ConnectionString, sql, ct));
            Assert.True(error is PostgresException, $"Expected the database to refuse '{sql}', got {(error is null ? "success" : error.GetType().Name + ": " + error.Message)}.");

            // Refused by the insert-only trigger, not by a foreign key (0A000: cannot truncate a referenced table).
            Assert.NotEqual(PostgresErrorCodes.FeatureNotSupported, ((PostgresException)error!).SqlState, StringComparer.Ordinal);
        }

        Assert.Equal(1, await LedgerRows.CountEntriesAsync(module.ConnectionString, DebitId(bet), ct));
        var amounts = await LedgerRows.PostingAmountsAsync(module.ConnectionString, DebitId(bet), ct);
        Assert.Equal(2, amounts.Count);
        Assert.Equal(BrlBalance(2_990.00m), await module.WithWalletAsync(wallet => wallet.GetBalanceAsync(player, Brl, ct)));
    }
}
