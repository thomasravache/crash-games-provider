using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// What a room may see of a live bet (SPEC-0025 §6, ADR-0005): an anonymized nickname (two first letters + <c>***</c>, or
/// <c>Jogador***</c>) and an opaque <c>betRef</c>; never a <c>betId</c>, a full nickname or a pseudonymous player id.
/// </summary>
internal static partial class LiveBetsPrivacy
{
    /// <summary>Fails when <paramref name="payload"/> carries a <c>betId</c> property, a bet id, a full nickname or a player id anywhere.</summary>
    public static void AssertNothingLeaks(
        JsonElement payload,
        IEnumerable<Guid> betIds,
        IEnumerable<string> nicknames,
        IEnumerable<string> players,
        string context)
    {
        ArgumentNullException.ThrowIfNull(betIds);
        ArgumentNullException.ThrowIfNull(nicknames);
        ArgumentNullException.ThrowIfNull(players);
        var raw = payload.GetRawText();
        Assert.False(HasProperty(payload, "betId"), $"{context}: the room must not see a betId property: {raw}");
        foreach (var betId in betIds)
        {
            foreach (var form in Forms(betId))
            {
                Assert.False(raw.Contains(form, StringComparison.OrdinalIgnoreCase), $"{context}: the bet id {betId} leaked to the room: {raw}");
            }
        }

        foreach (var nickname in nicknames)
        {
            Assert.False(raw.Contains(nickname, StringComparison.OrdinalIgnoreCase), $"{context}: the full nickname '{nickname}' leaked to the room: {raw}");
        }

        foreach (var player in players)
        {
            Assert.False(raw.Contains(player, StringComparison.OrdinalIgnoreCase), $"{context}: the player id '{player}' leaked to the room: {raw}");
        }
    }

    /// <summary>Fails when <paramref name="betRef"/> is empty, is a bet id or contains one.</summary>
    public static void AssertOpaqueRef(string? betRef, IEnumerable<Guid> betIds, string context)
    {
        ArgumentNullException.ThrowIfNull(betIds);
        Assert.False(string.IsNullOrWhiteSpace(betRef), $"{context}: betRef is required.");
        foreach (var betId in betIds)
        {
            foreach (var form in Forms(betId))
            {
                Assert.False(betRef!.Contains(form, StringComparison.OrdinalIgnoreCase), $"{context}: betRef '{betRef}' is (or contains) the bet id {betId}.");
            }
        }
    }

    /// <summary>Fails when <paramref name="nickname"/> is not an anonymized nickname.</summary>
    public static void AssertAnonymized(string? nickname, string context) =>
        Assert.True(nickname is not null && Anonymized().IsMatch(nickname), $"{context}: '{nickname}' is not anonymized (two first letters + ***, or Jogador***).");

    private static IEnumerable<string> Forms(Guid id) => [id.ToString("D"), id.ToString("N")];

    private static bool HasProperty(JsonElement element, string name) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) || HasProperty(p.Value, name)),
            JsonValueKind.Array => element.EnumerateArray().Any(item => HasProperty(item, name)),
            _ => false,
        };

    [GeneratedRegex(@"^(Jogador|[^*]{1,2})\*\*\*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Anonymized();
}
