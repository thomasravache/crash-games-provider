using System.Security.Claims;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;

namespace Apogeu.Wallet.Features.Sessions;

/// <summary>
/// What the wallet reads from the player session of SPEC-0009 §4 (claims <c>sub</c>, <c>op</c>, <c>cur</c> and
/// <c>mode</c>): the player, always the session's own (a player never reads or changes another's balance), its currency and mode.
/// </summary>
internal sealed record WalletSession(PlayerRef Player, Currency Currency, string Mode)
{
    public const string DemoMode = "demo";

    private const string SubjectClaim = "sub";
    private const string OperatorClaim = "op";
    private const string CurrencyClaim = "cur";
    private const string ModeClaim = "mode";

    public bool IsDemo => string.Equals(Mode, DemoMode, StringComparison.Ordinal);

    /// <summary>The session of an authenticated player (the player-session policy guarantees the claims).</summary>
    public static WalletSession Of(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new WalletSession(
            new PlayerRef(new OperatorId(Required(user, OperatorClaim)), new PlayerId(Required(user, SubjectClaim))),
            new Currency(Required(user, CurrencyClaim)),
            Required(user, ModeClaim));
    }

    private static string Required(ClaimsPrincipal user, string type) =>
        user.FindFirst(type)?.Value ?? throw new InvalidOperationException($"The player session has no '{type}' claim.");
}
