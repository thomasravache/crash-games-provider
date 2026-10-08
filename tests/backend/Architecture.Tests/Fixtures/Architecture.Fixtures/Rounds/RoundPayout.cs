namespace Apogeu.Rounds.Domain;

/// <summary>Known violation (UT-04): a money-named property typed double in Rounds.Domain (ADR-0002).</summary>
public sealed class RoundPayout
{
    public double PayoutAmount { get; init; }
}
