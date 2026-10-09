using System.Text.RegularExpressions;
using Apogeu.Operators.Features.Launch;
using Apogeu.TestKit.Time;
using Xunit;

namespace Apogeu.Operators.Tests.Unit;

/// <summary>UT-03: a launch token is single use and lives 60 s (SPEC-0009 §4, ADR-0015), on a frozen clock.</summary>
public sealed partial class LaunchGrantTests
{
    private static readonly LaunchSubject Subject = new("demo", "demo", "player-ut03", "Pipeiro", "BRL", "demo", "pt-BR");

    [Fact(DisplayName = "Dado um token emitido, quando usado duas vezes, então o segundo uso falha com USED")]
    [Trait("Spec", "SPEC-0009:UT-03")]
    public void GivenIssuedTokenWhenRedeemedTwiceThenSecondRedemptionIsAlreadyUsed()
    {
        var clock = TestClock.Create();
        var token = LaunchToken.Generate();
        var grant = LaunchGrant.Issue(token.Hash, Subject, clock.GetUtcNow());

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(LaunchRedemption.Redeemed, grant.Redeem(clock.GetUtcNow()));
        Assert.True(grant.IsUsed);
        Assert.Equal(LaunchRedemption.AlreadyUsed, grant.Redeem(clock.GetUtcNow()));
    }

    [Fact(DisplayName = "Dado um token emitido, quando usado após 61 s, então EXPIRED; a 60 s ainda vale")]
    [Trait("Spec", "SPEC-0009:UT-03")]
    public void GivenIssuedTokenWhenRedeemedAfter61SecondsThenExpired()
    {
        var clock = TestClock.Create();
        var issuedAt = clock.GetUtcNow();
        var expired = LaunchGrant.Issue(LaunchToken.Generate().Hash, Subject, issuedAt);
        var boundary = LaunchGrant.Issue(LaunchToken.Generate().Hash, Subject, issuedAt);

        Assert.Equal(issuedAt.AddSeconds(60), expired.ExpiresAt);
        Assert.Equal(LaunchRedemption.Redeemed, boundary.Redeem(issuedAt.AddSeconds(60)));
        Assert.Equal(LaunchRedemption.Expired, expired.Redeem(issuedAt.AddSeconds(61)));
        Assert.False(expired.IsUsed);
    }

    [Fact(DisplayName = "Dado um token gerado, então tem 32 bytes aleatórios em texto seguro para URL, só o hash é guardado e o texto não aparece em ToString")]
    [Trait("Spec", "SPEC-0009:UT-03")]
    public void GivenGeneratedTokensThenTheyAreRandomUrlSafeAndOnlyTheHashIsKept()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => LaunchToken.Generate()).ToList();

        Assert.Equal(tokens.Count, tokens.Select(t => t.Value).Distinct(StringComparer.Ordinal).Count());
        foreach (var token in tokens)
        {
            // 32 bytes are at least 43 characters in base64url (64 in hex); only URL-safe characters.
            Assert.Matches(UrlSafe(), token.Value);
            Assert.True(token.Value.Length >= 43, $"A 32-byte token needs at least 43 URL-safe characters, got {token.Value.Length}.");
            Assert.Equal(LaunchToken.HashOf(token.Value), token.Hash, StringComparer.Ordinal);
            Assert.NotEqual(token.Value, token.Hash, StringComparer.Ordinal);
            Assert.DoesNotContain(token.Value, token.ToString(), StringComparison.Ordinal);
        }

        var grant = LaunchGrant.Issue(tokens[0].Hash, Subject, TestClock.Epoch);
        Assert.Equal(tokens[0].Hash, grant.TokenHash, StringComparer.Ordinal);
        Assert.Equal(Subject, grant.Subject);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UrlSafe();
}
