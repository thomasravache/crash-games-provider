using Apogeu.BuildingBlocks.Security;
using Apogeu.Wallet.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apogeu.Wallet.Features.Errors;

/// <summary>Error responses of the wallet REST API (SPEC-0021 §6): <see cref="ErrorResponse"/> as <c>application/problem+json</c>, as in SPEC-0009.</summary>
internal static class WalletProblems
{
    public static JsonHttpResult<ErrorResponse> RechargeNotAllowed() =>
        Of(StatusCodes.Status409Conflict, WalletErrorCodes.RechargeNotAllowed, "The demo balance still covers the minimum bet.");

    public static JsonHttpResult<ErrorResponse> NotDemo() =>
        Of(StatusCodes.Status403Forbidden, WalletErrorCodes.NotDemo, "The demo recharge is only available in demo sessions.");

    private static JsonHttpResult<ErrorResponse> Of(int status, string code, string title) =>
        TypedResults.Json(new ErrorResponse(title, status, code), statusCode: status, contentType: ErrorResponse.ContentType);
}
