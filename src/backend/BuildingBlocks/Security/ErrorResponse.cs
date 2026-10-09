namespace Apogeu.BuildingBlocks.Security;

/// <summary>
/// Error body of the security contract v1 (SPEC-0009 §6), sent as <c>application/problem+json</c>: the RFC 9457 members
/// <c>title</c> and <c>status</c>, the contract error <c>code</c> (<see cref="SecurityErrorCodes"/>) and, for
/// <see cref="SecurityErrorCodes.ValidationError"/>, the invalid fields.
/// </summary>
/// <param name="Title">Short, human-readable summary; never carries a secret, a token or personal data.</param>
/// <param name="Status">HTTP status code of the response.</param>
/// <param name="Code">Contract error code.</param>
/// <param name="Errors">Invalid fields and their messages, only for validation errors.</param>
public sealed record ErrorResponse(string Title, int Status, string Code, IReadOnlyDictionary<string, string[]>? Errors = null)
{
    /// <summary>Media type of every error body (RFC 9457).</summary>
    public const string ContentType = "application/problem+json";
}
