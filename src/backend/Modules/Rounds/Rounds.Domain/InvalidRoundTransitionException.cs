namespace Apogeu.Rounds.Domain;

/// <summary>Domain error: the requested transition is not allowed from the current state of the round.</summary>
public sealed class InvalidRoundTransitionException : InvalidOperationException
{
    public InvalidRoundTransitionException()
    {
    }

    public InvalidRoundTransitionException(string message)
        : base(message)
    {
    }

    public InvalidRoundTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
