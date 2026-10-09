using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.Salas;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using ContractMultiplier = Apogeu.BuildingBlocks.Contracts.Multiplier;

namespace Apogeu.Rounds.Features.History;

/// <summary><c>GET /api/v1/salas/{salaId}/rounds?limit=1..100</c>: the last crashed rounds of the room, newest first.</summary>
internal static class GetSalaRoundsEndpoint
{
    public const string Route = "/api/v1/salas/{salaId}/rounds";

    public const int MinLimit = 1;

    public const int MaxLimit = 100;

    public const int DefaultLimit = 20;

    public static IEndpointRouteBuilder MapGetSalaRounds(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(Route, HandleAsync)
            .WithName("GetSalaRounds")
            .WithTags("Rounds")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<RoundHistoryItem[]>, ProblemHttpResult>> HandleAsync(
        string salaId,
        int? limit,
        SalaCatalog salas,
        IRoundStore store,
        CancellationToken ct)
    {
        if (!salas.TryGet(salaId, out var sala))
        {
            return RoundsProblems.SalaNotFound();
        }

        if (limit is < MinLimit or > MaxLimit)
        {
            return RoundsProblems.InvalidLimit(MinLimit, MaxLimit);
        }

        var rounds = await store.ListCrashedAsync(sala.Id, limit ?? DefaultLimit, ct).ConfigureAwait(false);
        return TypedResults.Ok(rounds
            .Select(r => new RoundHistoryItem(r.Id.Value, r.Number, new ContractMultiplier(r.CrashPoint!.Value), r.CrashedAt!.Value))
            .ToArray());
    }
}
