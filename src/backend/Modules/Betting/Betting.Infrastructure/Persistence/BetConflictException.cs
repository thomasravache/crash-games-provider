namespace Apogeu.Betting.Infrastructure.Persistence;

/// <summary>The bet collided with another one already committed (<see cref="Conflict"/>).</summary>
public sealed class BetConflictException : Exception
{
    public BetConflictException()
    {
    }

    public BetConflictException(string message)
        : base(message)
    {
    }

    public BetConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public BetConflictException(BetConflict conflict, Exception innerException)
        : base($"The bet collides with another one ({conflict}).", innerException)
    {
        Conflict = conflict;
    }

    public BetConflict Conflict { get; }
}
