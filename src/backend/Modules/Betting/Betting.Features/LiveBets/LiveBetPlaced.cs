using Apogeu.Betting.Domain;
using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Betting.Features.LiveBets;

/// <summary>A bet accepted in a round, as fed to the <see cref="LiveBetsBatcher"/> (SPEC-0025 §6).</summary>
/// <param name="SalaId">Room of the round.</param>
/// <param name="RoundId">Round of the bet.</param>
/// <param name="BetId">Internal id of the bet; never broadcast (the room sees an opaque <c>betRef</c>).</param>
/// <param name="Owner">Player of the bet; counts for the distinct players and gives the currency.</param>
/// <param name="Nickname">Nickname of the session; anonymized before it is broadcast.</param>
/// <param name="Amount">Amount of the bet.</param>
public sealed record LiveBetPlaced(string SalaId, Guid RoundId, Guid BetId, BetOwner Owner, string? Nickname, Money Amount);
