using System.Security.Claims;
using Apogeu.Betting.Features.Sessions;
using Xunit;

namespace Apogeu.Betting.Tests.Unit;

/// <summary>UT-03, Betting side (SPEC-0025 Emenda v2): the session of the hub and of the REST API exposes the claim <c>nick</c>.</summary>
public sealed class BettingSessionNicknameTests
{
    [Fact(DisplayName = "Dada uma sessão com a claim nick \"Marcos\", quando o Betting lê a sessão, então expõe o apelido \"Marcos\"")]
    [Trait("Spec", "SPEC-0025:UT-03")]
    public void GivenSessionWithNickClaimWhenReadThenNicknameIsExposed()
    {
        var session = BettingSession.From(Principal(("nick", "Marcos")));

        Assert.Equal("Marcos", session.Nickname);
        Assert.Equal("player-1", session.Owner.PlayerId.Value);
        Assert.Equal("demo", session.Owner.OperatorId.Value);
    }

    [Fact(DisplayName = "Dada uma sessão sem a claim nick, quando o Betting lê a sessão, então o apelido é nulo")]
    [Trait("Spec", "SPEC-0025:UT-03")]
    public void GivenSessionWithoutNickClaimWhenReadThenNicknameIsNull()
    {
        var session = BettingSession.From(Principal());

        Assert.Null(session.Nickname);
        Assert.Equal("player-1", session.Owner.PlayerId.Value);
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] extra)
    {
        Claim[] claims =
        [
            new("sub", "player-1"),
            new("op", "demo"),
            new("sala", "demo"),
            new("cur", "BRL"),
            new("mode", "demo"),
            .. extra.Select(c => new Claim(c.Type, c.Value)),
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "PlayerSession"));
    }
}
