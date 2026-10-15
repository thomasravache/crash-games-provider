using System.Net;
using Apogeu.DemoCasino.Tests.Support;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Unit;

// SPEC-0043 UT-01: the entry rules of the contract (§6) at the boundary of the BFF — nickname of 3–20 letters, digits
// or _, currency "BRL" only. Exercised through POST /casino/session on the in-memory DemoCasino (no external dependency),
// so the test holds whatever validator type the implementation uses.
public sealed class SessionEntryValidationTests(DemoCasinoFixture casino) : IClassFixture<DemoCasinoFixture>
{
    private const string Brl = "BRL";

    public static TheoryData<string> ValidNicknames =>
    [
        "abc",
        "Jogador_01",
        "___",
        "007",
        "abcdefghijklmnopqrst", // 20 characters
    ];

    public static TheoryData<string> InvalidNicknames =>
    [
        "",
        "a",
        "ab",
        "abcdefghijklmnopqrstu", // 21 characters
        "com espaco",
        " abc",
        "abc ",
        "jogador-1",
        "jogador.1",
        "jogador@1",
        "<script>",
    ];

    public static TheoryData<string> UnsupportedCurrencies => ["USD", "EUR", ""];

    [Theory(DisplayName = "Dado um apelido válido e BRL, quando entra, então 200 com playerId demo-<uuid> e o apelido")]
    [Trait("Spec", "SPEC-0043:UT-01")]
    [MemberData(nameof(ValidNicknames))]
    public async Task GivenValidNicknameAndBrlWhenEnteringThenSessionIsCreated(string nickname)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = casino.Host.CreateRawClient();

        using var response = await CasinoApi.EnterAsync(client, nickname, Brl, ct);

        var body = await CasinoApi.ReadOkJsonAsync(response, ct);
        Assert.Equal(nickname, body.GetProperty("nickname").GetString());
        var playerId = body.GetProperty("playerId").GetString() ?? string.Empty;
        Assert.StartsWith("demo-", playerId, StringComparison.Ordinal);
        Assert.True(Guid.TryParse(playerId["demo-".Length..], out _), $"playerId must be demo-<uuid>, got '{playerId}'.");
        Assert.NotNull(CasinoApi.SessionSetCookie(response));
    }

    [Theory(DisplayName = "Dado um apelido inválido, quando entra, então 400 INVALID_NICKNAME sem cookie de sessão")]
    [Trait("Spec", "SPEC-0043:UT-01")]
    [MemberData(nameof(InvalidNicknames))]
    public async Task GivenInvalidNicknameWhenEnteringThenInvalidNickname(string nickname)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = casino.Host.CreateRawClient();

        using var response = await CasinoApi.EnterAsync(client, nickname, Brl, ct);

        await CasinoApi.AssertErrorAsync(response, HttpStatusCode.BadRequest, "INVALID_NICKNAME", ct);
        Assert.Null(CasinoApi.SessionSetCookie(response));
    }

    [Theory(DisplayName = "Dada uma moeda diferente de BRL, quando entra, então 400 CURRENCY_NOT_SUPPORTED sem cookie de sessão")]
    [Trait("Spec", "SPEC-0043:UT-01")]
    [MemberData(nameof(UnsupportedCurrencies))]
    public async Task GivenUnsupportedCurrencyWhenEnteringThenCurrencyNotSupported(string currency)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = casino.Host.CreateRawClient();

        using var response = await CasinoApi.EnterAsync(client, CasinoApi.NewNickname(), currency, ct);

        await CasinoApi.AssertErrorAsync(response, HttpStatusCode.BadRequest, "CURRENCY_NOT_SUPPORTED", ct);
        Assert.Null(CasinoApi.SessionSetCookie(response));
    }
}
