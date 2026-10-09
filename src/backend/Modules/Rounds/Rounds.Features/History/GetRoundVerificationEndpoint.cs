using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.Live;
using Apogeu.Rounds.Features.Salas;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using ContractMultiplier = Apogeu.BuildingBlocks.Contracts.Multiplier;

namespace Apogeu.Rounds.Features.History;

/// <summary>
/// <c>GET /api/v1/rounds/{roundId}/verification</c>: the data SPEC-0007's <c>Verify</c> accepts for a crashed round. A round
/// still in play is <c>409 ROUND_NOT_FINISHED</c> and exposes nothing (SPEC-0008 §6).
/// </summary>
internal static class GetRoundVerificationEndpoint
{
    public const string Route = "/api/v1/rounds/{roundId:guid}/verification";

    public static IEndpointRouteBuilder MapGetRoundVerification(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(Route, HandleAsync)
            .WithName("GetRoundVerification")
            .WithTags("Rounds")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<Results<Ok<RoundVerificationResponse>, ProblemHttpResult>> HandleAsync(
        Guid roundId,
        SalaCatalog salas,
        IRoundStore rounds,
        IRoundChainStore chains,
        CancellationToken ct)
    {
        var round = await rounds.FindAsync(new RoundId(roundId), ct).ConfigureAwait(false);
        if (round is null)
        {
            return RoundsProblems.RoundNotFound();
        }

        if (round.State != RoundState.Crashed)
        {
            return RoundsProblems.RoundNotFinished();
        }

        var chain = await chains.FindAsync(round.ChainId, ct).ConfigureAwait(false);
        if (chain is null || !salas.TryGet(round.SalaId, out var sala))
        {
            return RoundsProblems.RoundNotFound();
        }

        // previousSeed = SHA256(s_r): the commitment published before the bets.
        return TypedResults.Ok(new RoundVerificationResponse(
            round.RevealedSeed!,
            round.Commitment,
            chain.Salt,
            SalaSnapshotQuery.FormatRtp(sala.Rtp),
            new ContractMultiplier(sala.Cap),
            new ContractMultiplier(round.CrashPoint!.Value),
            chain.TerminalHash));
    }
}
