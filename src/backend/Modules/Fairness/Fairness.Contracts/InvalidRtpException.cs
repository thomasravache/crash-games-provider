namespace Apogeu.Fairness.Contracts;

/// <summary>RTP outside [0.90, 0.99].</summary>
public sealed class InvalidRtpException : FairnessException
{
    public InvalidRtpException()
    {
    }

    public InvalidRtpException(string message)
        : base(message)
    {
    }

    public InvalidRtpException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
