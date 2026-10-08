namespace Apogeu.Betting.Domain;

/// <summary>Known violation (UT-04): monetary property typed double (ADR-0002 requires decimal).</summary>
public sealed class FloatingStake
{
    public double Amount { get; init; }
}
