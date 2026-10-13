using System.Globalization;
using System.Text.Json;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Security.Tests.Support;
using Apogeu.TestKit.Containers;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static Apogeu.Wallet.Tests.Support.SeamlessExpectations;
using static Apogeu.Wallet.Tests.Support.WalletExpectations;

namespace Apogeu.Wallet.Tests.Contract;

/// <summary>
/// CT-01 (SPEC-0046 §7.4): the real requests the adapter sends to the operator test server — balance, debit (with and without
/// round), credit and rollback — checked against the public contract (docs/integration/operator-wallet-api.md §2–§4): POST
/// JSON to <c>{walletUrl}/apogeu/v1/{op}</c>, headers <c>X-Apogeu-Key</c>/<c>X-Apogeu-Timestamp</c>/<c>X-Apogeu-Signature</c>
/// with the reference HMAC over the exact bytes, and the fields of each body (amounts as two-decimal strings).
/// </summary>
[Collection(WalletPostgres.Name)]
public sealed class OperatorWalletSignatureContractTests(PostgreSqlContainerFixture postgres)
{
    [Fact(DisplayName = "Dadas as requisições reais enviadas ao operador de teste, quando verificadas com a assinatura de referência do contrato público, então conferem (cabeçalhos, caminho, corpo)")]
    [Trait("Spec", "SPEC-0046:CT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenRealRequestsToOperatorWhenVerifiedWithReferenceSignatureThenTheyConform()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stub = await OperatorWalletStub.StartAsync(ct);
        var key = TestApiKey.Generate();
        await using var module = await WalletServices.StartAsync(postgres.ConnectionString, ct, services =>
        {
            services.RemoveAll<IOperatorWalletSettings>();
            services.AddSingleton<IOperatorWalletSettings>(SeamlessOn(stub, key));
        });
        var player = NewCasinoPlayer();
        var bet = Guid.NewGuid();
        var queuedBet = Guid.NewGuid();
        var round = new RoundId(Guid.NewGuid());

        var balance = await module.WithWalletAsync(wallet => wallet.GetBalanceAsync(player, Brl, ct));
        AssertOk(await module.WithWalletAsync(wallet => wallet.DebitAsync(Debit(player, 10.00m, bet, round), ct)), 2_990.00m);
        AssertOk(await module.WithWalletAsync(wallet => wallet.CreditAsync(Credit(player, 25.50m, bet, round), ct)), 3_015.50m);
        var queued = new WalletOperation(DebitId(queuedBet), player, Brl, new Money(5.00m), RoundId: null, queuedBet);
        AssertOk(await module.WithWalletAsync(wallet => wallet.DebitAsync(queued, ct)), 3_010.50m);
        var rollback = new WalletRollback(RollbackId(queuedBet), DebitId(queuedBet), player, Brl, RoundId: null, queuedBet);
        AssertOk(await module.WithWalletAsync(wallet => wallet.RollbackAsync(rollback, ct)), 3_015.50m);

        Assert.Equal(BrlBalance(OperatorWalletStub.OpeningBalance), balance);
        var requests = stub.Received;
        Assert.Equal(
            string.Join(' ', BalancePath, DebitPath, CreditPath, DebitPath, RollbackPath),
            string.Join(' ', requests.Select(r => r.Path)));
        foreach (var request in requests)
        {
            Assert.Equal("POST", request.Method);
            Assert.StartsWith("application/json", request.ContentType, StringComparison.OrdinalIgnoreCase);
            AssertSigned(request, key);
            AssertNotSignedWith(request, TestApiKey.Generate());
        }

        AssertBodies(requests, player, bet, queuedBet, round);
    }

    private static void AssertBodies(IReadOnlyList<StubRequest> requests, PlayerRef player, Guid bet, Guid queuedBet, RoundId round)
    {
        var playerId = player.PlayerId.Value;
        AssertBody(requests[0].Json, Fields(("playerId", playerId), ("currency", "BRL")));
        AssertBody(requests[1].Json, Fields(
            ("transactionId", DebitId(bet).Value), ("playerId", playerId), ("currency", "BRL"), ("amount", "10.00"),
            ("roundId", round.Value.ToString("D")), ("betId", bet.ToString("D"))));
        AssertBody(requests[2].Json, Fields(
            ("transactionId", CreditId(bet).Value), ("playerId", playerId), ("currency", "BRL"), ("amount", "25.50"),
            ("roundId", round.Value.ToString("D")), ("betId", bet.ToString("D"))));
        AssertBody(requests[3].Json, Fields(
            ("transactionId", DebitId(queuedBet).Value), ("playerId", playerId), ("currency", "BRL"), ("amount", "5.00"),
            ("roundId", null), ("betId", queuedBet.ToString("D"))));
        AssertBody(requests[4].Json, Fields(
            ("transactionId", RollbackId(queuedBet).Value), ("originalTransactionId", DebitId(queuedBet).Value), ("playerId", playerId),
            ("currency", "BRL"), ("roundId", null), ("betId", queuedBet.ToString("D"))));
    }

    private static Dictionary<string, string?> Fields(params (string Name, string? Value)[] fields) =>
        fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal);

    /// <summary>A signature of another secret does not match: the check above is not vacuous.</summary>
    private static void AssertNotSignedWith(StubRequest request, TestApiKey other)
    {
        var timestamp = long.Parse(request.Timestamp!, CultureInfo.InvariantCulture);
        Assert.False(string.Equals(
            ReferenceSignature.Compute(other.SecretBytes, timestamp, request.Method, request.Path, request.Body),
            request.Signature,
            StringComparison.Ordinal));
    }

    /// <summary>
    /// The body has exactly the expected properties (public contract §4): strings where a value is given and <c>null</c> (or
    /// absent) where <see langword="null"/> is expected; <c>amount</c> is never present in <c>balance</c> and <c>rollback</c>.
    /// </summary>
    private static void AssertBody(JsonElement body, Dictionary<string, string?> expected)
    {
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        var actual = body.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
        foreach (var name in actual.Keys)
        {
            Assert.True(expected.ContainsKey(name), $"Unexpected property '{name}' in {body}.");
        }

        foreach (var (name, value) in expected)
        {
            if (value is null)
            {
                Assert.True(
                    !actual.TryGetValue(name, out var present) || present.ValueKind == JsonValueKind.Null,
                    $"Expected '{name}' null or absent in {body}.");
                continue;
            }

            Assert.True(actual.TryGetValue(name, out var property), $"Missing '{name}' in {body}.");
            Assert.True(property.ValueKind == JsonValueKind.String, $"'{name}' must be a JSON string (amounts never as numbers) in {body}.");
            Assert.Equal(value, property.GetString());
        }
    }
}
