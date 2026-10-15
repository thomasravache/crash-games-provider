using System.Globalization;
using System.Net;
using System.Text;
using Apogeu.DemoCasino.Tests.Support;
using Apogeu.Security.Tests.Support;
using Xunit;
using static Apogeu.DemoCasino.Tests.Support.CasinoWalletApi;

namespace Apogeu.DemoCasino.Tests.Integration;

// SPEC-0047 IT-02 (§5 Segurança): every call to the wallet API of the DemoCasino must carry the Apogeu signature of the
// public contract (§3: X-Apogeu-Key, X-Apogeu-Timestamp within ±300 s, X-Apogeu-Signature over timestamp, method, path
// and the SHA-256 of the exact body); without it, or with a wrong one, the casino answers 401 and processes nothing. The
// real DemoCasino in memory (its whole HTTP pipeline); the signature of reference is the independent one of SPEC-0009.
public sealed class CasinoWalletSignatureTests
{
    public static TheoryData<string> Rejections =>
    [
        "sem cabeçalhos",
        "sem assinatura",
        "assinatura de outro corpo",
        "assinatura com outro segredo",
        "chave desconhecida",
        "timestamp 301 s no passado",
        "timestamp 301 s no futuro",
    ];

    [Theory(DisplayName = "Dada uma chamada sem assinatura ou com assinatura inválida, quando chega ao DemoCasino, então 401 e nada é processado")]
    [Trait("Spec", "SPEC-0047:IT-02")]
    [Trait("Category", "Integration")]
    [MemberData(nameof(Rejections))]
    public async Task GivenUnsignedOrBadlySignedCallWhenItReachesCasinoThenUnauthorizedAndNothingProcessed(string rejection)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var casino = DemoCasinoHost.Start();
        using var browser = casino.CreateBrowserClient();
        var playerId = await EnterAsync(browser, ct);
        using var apogeu = casino.CreateRawClient();
        var debit = Debit(playerId, 10.00m, Guid.NewGuid());

        using var response = await SendRejectedAsync(apogeu, casino, debit, rejection, ct);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 for a call {rejection}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");

        // Nothing was processed: the balance is untouched and the same debit, now signed, is applied for the first time.
        Assert.Equal(OpeningBalance, Amount((await CallAsync(apogeu, Balance(playerId), casino.OperatorKeyId, casino.OperatorSecret, ct)).Balance));
        var applied = await CallAsync(apogeu, debit, casino.OperatorKeyId, casino.OperatorSecret, ct);
        Assert.Equal("OK", applied.Status);
        Assert.Equal(2_990.00m, Amount(applied.Balance));
    }

    [Fact(DisplayName = "Dada uma chamada de saldo sem assinatura, quando chega ao DemoCasino, então 401 sem saldo na resposta")]
    [Trait("Spec", "SPEC-0047:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenUnsignedBalanceCallWhenItReachesCasinoThenUnauthorizedWithoutBalance()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var casino = DemoCasinoHost.Start();
        using var browser = casino.CreateBrowserClient();
        var playerId = await EnterAsync(browser, ct);
        using var apogeu = casino.CreateRawClient();

        using var response = await SendAsync(apogeu, Balance(playerId), keyId: null, timestamp: null, signature: null, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"Expected 401, got {(int)response.StatusCode}: {text}");
        Assert.DoesNotContain("3000", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Dada uma chamada assinada corretamente, quando chega ao DemoCasino, então é aceita (200)")]
    [Trait("Spec", "SPEC-0047:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenWellSignedCallWhenItReachesCasinoThenAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var casino = DemoCasinoHost.Start();
        using var browser = casino.CreateBrowserClient();
        var playerId = await EnterAsync(browser, ct);
        using var apogeu = casino.CreateRawClient();

        // Within the ±300 s window on both sides.
        using var past = await SendSignedAsync(apogeu, Balance(playerId), casino.OperatorKeyId, casino.OperatorSecret, ct, DateTimeOffset.UtcNow.AddSeconds(-240));
        using var future = await SendSignedAsync(apogeu, Balance(playerId), casino.OperatorKeyId, casino.OperatorSecret, ct, DateTimeOffset.UtcNow.AddSeconds(240));

        Assert.Equal(HttpStatusCode.OK, past.StatusCode);
        Assert.Equal(HttpStatusCode.OK, future.StatusCode);
    }

    private static Task<HttpResponseMessage> SendRejectedAsync(HttpClient client, DemoCasinoHost casino, WalletCall call, string rejection, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var secret = Encoding.UTF8.GetBytes(casino.OperatorSecret);
        string Timestamp(DateTimeOffset at) => at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        string Sign(byte[] key, DateTimeOffset at, byte[] body) => ReferenceSignature.Compute(key, at.ToUnixTimeSeconds(), "POST", call.Path, body);

        return rejection switch
        {
            "sem cabeçalhos" => SendAsync(client, call, keyId: null, timestamp: null, signature: null, ct),
            "sem assinatura" => SendAsync(client, call, casino.OperatorKeyId, Timestamp(now), signature: null, ct),
            "assinatura de outro corpo" => SendAsync(client, call, casino.OperatorKeyId, Timestamp(now), Sign(secret, now, [.. call.Body, (byte)' ']), ct),
            "assinatura com outro segredo" => SendAsync(client, call, casino.OperatorKeyId, Timestamp(now), Sign(TestApiKey.Generate().SecretBytes, now, call.Body), ct),
            "chave desconhecida" => SendAsync(client, call, TestApiKey.Generate().Id, Timestamp(now), Sign(secret, now, call.Body), ct),
            "timestamp 301 s no passado" => SendSignedAsync(client, call, casino.OperatorKeyId, casino.OperatorSecret, ct, now.AddSeconds(-301)),
            "timestamp 301 s no futuro" => SendSignedAsync(client, call, casino.OperatorKeyId, casino.OperatorSecret, ct, now.AddSeconds(301)),
            _ => throw new ArgumentOutOfRangeException(nameof(rejection), rejection, "Unknown rejection case."),
        };
    }
}
