namespace Apogeu.Backoffice.Features.Meta;

/// <summary>Body of <c>GET /api/v1/meta</c> (SPEC-0006 §6): version of the public API and the server clock in UTC.</summary>
public sealed record MetaResponse(string ApiVersion, DateTimeOffset ServerTime)
{
    /// <summary>Version of the v1 API contract.</summary>
    public const string Current = "1.0.0";
}
