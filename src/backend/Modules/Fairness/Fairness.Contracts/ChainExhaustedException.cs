namespace Apogeu.Fairness.Contracts;

/// <summary>Every round of the room's active chain was issued; the caller creates a new chain.</summary>
public sealed class ChainExhaustedException : FairnessException
{
    public ChainExhaustedException()
    {
    }

    public ChainExhaustedException(string message)
        : base(message)
    {
    }

    public ChainExhaustedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
