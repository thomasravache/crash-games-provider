using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Apogeu.Rounds.Features.History;

/// <summary>REST errors of the module (SPEC-0008 §6): ProblemDetails (RFC 9457) with the extension <c>code</c>.</summary>
internal static class RoundsProblems
{
    public static ProblemHttpResult SalaNotFound() =>
        Problem(StatusCodes.Status404NotFound, RoundsErrorCodes.SalaNotFound, "The room does not exist.");

    public static ProblemHttpResult InvalidLimit(int min, int max) =>
        Problem(StatusCodes.Status400BadRequest, RoundsErrorCodes.InvalidLimit, $"The limit must be between {min} and {max}.");

    public static ProblemHttpResult RoundNotFound() =>
        Problem(StatusCodes.Status404NotFound, RoundsErrorCodes.RoundNotFound, "The round does not exist.");

    public static ProblemHttpResult RoundNotFinished() =>
        Problem(StatusCodes.Status409Conflict, RoundsErrorCodes.RoundNotFinished, "The round has not crashed yet: its seed is not revealed.");

    private static ProblemHttpResult Problem(int status, string code, string detail) =>
        TypedResults.Problem(new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            Extensions = { ["code"] = code },
        });
}
