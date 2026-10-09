namespace Apogeu.Fairness.Contracts;

/// <summary>Cap below 1.00.</summary>
public sealed class InvalidCapException : FairnessException
{
    public InvalidCapException()
    {
    }

    public InvalidCapException(string message)
        : base(message)
    {
    }

    public InvalidCapException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
