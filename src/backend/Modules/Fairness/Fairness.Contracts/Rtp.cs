namespace Apogeu.Fairness.Contracts;

/// <summary>Return to player of a room, an exact decimal in [0.90, 0.99]; outside the range throws <see cref="InvalidRtpException"/>.</summary>
public readonly record struct Rtp
{
    /// <summary>Lowest RTP allowed (SPEC-0007 §4).</summary>
    public const decimal Minimum = 0.90m;

    /// <summary>Highest RTP allowed (SPEC-0007 §4).</summary>
    public const decimal Maximum = 0.99m;

    public Rtp(decimal value)
    {
        if (value is < Minimum or > Maximum)
        {
            throw new InvalidRtpException($"The RTP must be between {Minimum} and {Maximum}; {value} was given.");
        }

        Value = value;
    }

    public decimal Value { get; }
}
