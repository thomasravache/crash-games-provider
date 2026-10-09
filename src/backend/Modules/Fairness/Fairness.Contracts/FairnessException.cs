namespace Apogeu.Fairness.Contracts;

/// <summary>Base of the errors of the Fairness contract v1 (SPEC-0007 §6).</summary>
public abstract class FairnessException : Exception
{
    protected FairnessException()
    {
    }

    protected FairnessException(string message)
        : base(message)
    {
    }

    protected FairnessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
