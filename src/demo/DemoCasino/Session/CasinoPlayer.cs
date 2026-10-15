namespace Apogeu.DemoCasino.Session;

/// <summary>The player of a casino session: what the signed cookie carries (no operator credential, no personal data).</summary>
internal sealed record CasinoPlayer(string PlayerId, string Nickname, string Currency);
