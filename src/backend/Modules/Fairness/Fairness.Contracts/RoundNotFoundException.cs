namespace Apogeu.Fairness.Contracts;

/// <summary>The chain or the round does not exist (or the round was not issued yet).</summary>
public sealed class RoundNotFoundException : FairnessException
{
    public RoundNotFoundException()
    {
    }

    public RoundNotFoundException(string message)
        : base(message)
    {
    }

    public RoundNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
