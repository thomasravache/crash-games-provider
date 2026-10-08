namespace Apogeu.Fairness.Contracts;

#pragma warning disable MA0025 // Scaffold of SPEC-0007 (no logic yet): every member is implemented in the green phase.

/// <summary>Return to player of a room, an exact decimal in [0.90, 0.99]; outside the range throws <see cref="InvalidRtpException"/>.</summary>
public readonly record struct Rtp
{
    public Rtp(decimal value)
    {
        throw new NotImplementedException();
    }

    public decimal Value { get; }
}
#pragma warning restore MA0025
