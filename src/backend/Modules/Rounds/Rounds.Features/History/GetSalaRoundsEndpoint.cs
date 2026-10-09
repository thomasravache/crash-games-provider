using System.Globalization;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.Salas;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;
using ContractMultiplier = Apogeu.BuildingBlocks.Contracts.Multiplier;

namespace Apogeu.Rounds.Features.History;

/// <summary><c>GET /api/v1/salas/{salaId}/rounds?limit=1..100</c>: the last crashed rounds of the room, newest first.</summary>
internal static class GetSalaRoundsEndpoint
{
    public const string Route = "/api/v1/salas/{salaId}/rounds";

    public const int MinLimit = 1;

    public const int MaxLimit = 100;

    public const int DefaultLimit = 20;

    private const string LimitParameter = "limit";

    public static IEndpointRouteBuilder MapGetSalaRounds(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(Route, HandleAsync)
            .WithName("GetSalaRounds")
            .WithTags("Rounds")
            .AddOpenApiOperationTransformer(DescribeLimitAsInteger)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>
    /// <c>limit</c> is bound as text so that a non-integer one (<c>limit=abc</c>) is the contract error
    /// <c>INVALID_LIMIT</c> instead of the framework's binding failure; the document keeps describing it as an integer.
    /// </summary>
    private static Task DescribeLimitAsInteger(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        if (operation.Parameters?.OfType<OpenApiParameter>().FirstOrDefault(p => string.Equals(p.Name, LimitParameter, StringComparison.Ordinal)) is { } limit)
        {
            limit.Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" };
        }

        return Task.CompletedTask;
    }

    private static async Task<Results<Ok<RoundHistoryItem[]>, ProblemHttpResult>> HandleAsync(
        string salaId,
        [FromQuery(Name = LimitParameter)] string? limit,
        SalaCatalog salas,
        IRoundStore store,
        CancellationToken ct)
    {
        if (!salas.TryGet(salaId, out var sala))
        {
            return RoundsProblems.SalaNotFound();
        }

        if (!TryParseLimit(limit, out var count))
        {
            return RoundsProblems.InvalidLimit(MinLimit, MaxLimit);
        }

        var rounds = await store.ListCrashedAsync(sala.Id, count, ct).ConfigureAwait(false);
        return TypedResults.Ok(rounds
            .Select(r => new RoundHistoryItem(r.Id.Value, r.Number, new ContractMultiplier(r.CrashPoint!.Value), r.CrashedAt!.Value))
            .ToArray());
    }

    /// <summary>An absent <c>limit</c> is <see cref="DefaultLimit"/>; otherwise it must be an integer in 1..100.</summary>
    private static bool TryParseLimit(string? text, out int limit)
    {
        if (string.IsNullOrEmpty(text))
        {
            limit = DefaultLimit;
            return true;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out limit) && limit is >= MinLimit and <= MaxLimit;
    }
}
