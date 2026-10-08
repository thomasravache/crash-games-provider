using Microsoft.AspNetCore.Http;

namespace Apogeu.Rounds.Domain;

/// <summary>Known violation (IT-01 guard): Rounds.Domain depends on ASP.NET Core (ADR-0001).</summary>
public static class RoundHttpProbe
{
    public static string? ReadRoundId(HttpContext context) => context?.Request.Query["roundId"];
}
