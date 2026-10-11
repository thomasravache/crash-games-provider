using System.Security.Claims;
using Apogeu.Betting.Domain;
using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.Sessions;

/// <summary>
/// The player session of a hub connection (SPEC-0009 §4): the claims <c>sub</c>, <c>op</c>, <c>sala</c>, <c>cur</c> and
/// <c>mode</c> of the session token validated by the player-session scheme.
/// </summary>
/// <param name="Owner">Operator, player and currency of the bets.</param>
/// <param name="SalaId">Room of the session.</param>
/// <param name="Mode"><c>demo</c> or <c>real</c>, from the token.</param>
public sealed record BettingSession(BetOwner Owner, string SalaId, string Mode)
{
    /// <summary>The only mode enabled (SPEC-0022 §5: real money is refused).</summary>
    public const string DemoMode = "demo";

    public bool IsDemo => string.Equals(Mode, DemoMode, StringComparison.Ordinal);

    /// <summary>The session of <paramref name="user"/>; throws when a claim is missing (the hub requires the session policy).</summary>
    public static BettingSession From(ClaimsPrincipal? user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new BettingSession(
            new BetOwner(new OperatorId(Claim(user, "op")), new PlayerId(Claim(user, "sub")), new Currency(Claim(user, "cur"))),
            Claim(user, "sala"),
            Claim(user, "mode"));
    }

    private static string Claim(ClaimsPrincipal user, string type) =>
        user.FindFirst(type)?.Value ?? throw new InvalidOperationException($"The player session has no '{type}' claim.");
}
