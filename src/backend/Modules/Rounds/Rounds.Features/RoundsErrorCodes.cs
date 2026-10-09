namespace Apogeu.Rounds.Features;

/// <summary>
/// Error codes of the contract (SPEC-0008 §6): the <c>code</c> extension of the REST ProblemDetails and the message of the
/// hub's HubException.
/// </summary>
public static class RoundsErrorCodes
{
    public const string SalaNotFound = "SALA_NOT_FOUND";

    public const string InvalidLimit = "INVALID_LIMIT";

    public const string RoundNotFound = "ROUND_NOT_FOUND";

    public const string RoundNotFinished = "ROUND_NOT_FINISHED";
}
