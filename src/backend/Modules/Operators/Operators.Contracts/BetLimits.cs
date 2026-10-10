using Apogeu.BuildingBlocks.Contracts;

namespace Apogeu.Operators.Contracts;

/// <summary>Bet limits of an operator in one currency (SPEC-0020 §6); every amount is in <see cref="Currency"/>.</summary>
/// <param name="Currency">Currency of the amounts.</param>
/// <param name="MinBet">Smallest bet accepted.</param>
/// <param name="MaxBet">Largest bet accepted.</param>
/// <param name="MaxWin">Largest win paid for one bet.</param>
public sealed record BetLimits(Currency Currency, Money MinBet, Money MaxBet, Money MaxWin);
