namespace Apogeu.Fairness.Domain;

/// <summary>Known violation (IT-01 guard): Fairness.Domain reads the system clock (ADR-0001).</summary>
public static class SeedClock
{
    public static DateTime StampedAt() => DateTime.UtcNow;
}
