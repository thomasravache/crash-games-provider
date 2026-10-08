namespace Apogeu.Fairness.Contracts;

/// <summary>Seed that is not 32 bytes long.</summary>
public sealed class InvalidSeedException : FairnessException
{
    public InvalidSeedException()
    {
    }

    public InvalidSeedException(string message)
        : base(message)
    {
    }

    public InvalidSeedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
