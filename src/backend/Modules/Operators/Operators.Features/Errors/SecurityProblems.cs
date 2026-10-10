using Apogeu.BuildingBlocks.Security;
using Apogeu.Operators.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apogeu.Operators.Features.Errors;

/// <summary>
/// Error responses of the security contract v1 and of the catalog (SPEC-0020) (<see cref="ErrorResponse"/>,
/// <c>application/problem+json</c>).
/// </summary>
internal static class SecurityProblems
{
    public static JsonHttpResult<ErrorResponse> Of(int status, string code, IReadOnlyDictionary<string, string[]>? errors = null) =>
        TypedResults.Json(Body(status, code, errors), statusCode: status, contentType: ErrorResponse.ContentType);

    public static ErrorResponse Body(int status, string code, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(TitleOf(code), status, code, errors);

    private static string TitleOf(string code) => code switch
    {
        SecurityErrorCodes.ValidationError => "The request is invalid.",
        SecurityErrorCodes.UnknownKey => "The API key is unknown.",
        SecurityErrorCodes.InvalidSignature => "The request signature is invalid.",
        SecurityErrorCodes.TimestampOutOfWindow => "The request timestamp is outside the accepted window.",
        SecurityErrorCodes.RealMoneyDisabled => "Real money is disabled; only demo mode is available.",
        SecurityErrorCodes.ReplayDetected => "The signed request was already received.",
        SecurityErrorCodes.RateLimited => "Too many requests.",
        SecurityErrorCodes.LaunchTokenInvalid => "The launch token is invalid.",
        SecurityErrorCodes.LaunchTokenExpired => "The launch token has expired.",
        SecurityErrorCodes.LaunchTokenUsed => "The launch token was already used.",
        OperatorsErrorCodes.GameNotAvailable => "The game is not available for the operator.",
        OperatorsErrorCodes.CurrencyNotEnabled => "The currency is not enabled for the operator.",
        _ => "The request was refused.",
    };
}
