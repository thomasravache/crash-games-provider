using System.Globalization;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.Conductor;
using Apogeu.Rounds.Features.Salas;
using ContractMultiplier = Apogeu.BuildingBlocks.Contracts.Multiplier;

namespace Apogeu.Rounds.Features.Live;

/// <summary>
/// The snapshot of a room for <c>JoinSala</c> (SPEC-0008 §6): its public configuration, the current round and the last 20
/// crashed rounds. The current round carries its crash point and seed only once it crashed: the store has neither before.
/// </summary>
public sealed class SalaSnapshotQuery(SalaCatalog salas, IRoundStore store)
{
    public const int HistorySize = 20;

    /// <summary>Whether the room is configured.</summary>
    public bool Exists(string? salaId) => salas.TryGet(salaId, out _);

    /// <summary>The snapshot of the room, or <see langword="null"/> when the room does not exist.</summary>
    public async Task<RoundSnapshot?> GetAsync(string salaId, CancellationToken ct)
    {
        if (!salas.TryGet(salaId, out var sala))
        {
            return null;
        }

        var current = await store.FindLatestAsync(sala.Id, ct).ConfigureAwait(false);
        var history = await store.ListCrashedAsync(sala.Id, HistorySize, ct).ConfigureAwait(false);
        return new RoundSnapshot(
            Info(sala),
            current is null ? null : Current(current),
            history.Select(r => new RoundSummary(r.Id.Value, r.Number, new ContractMultiplier(r.CrashPoint!.Value))).ToArray());
    }

    /// <summary>The public configuration of the room: RTP with 2 decimals, the cap and the growth constant as text.</summary>
    public static SalaInfo Info(SalaConfig sala) => new(
        sala.Id,
        FormatRtp(sala.Rtp),
        new ContractMultiplier(sala.Cap),
        SalaConductor.FormatGrowthK(sala.GrowthK));

    /// <summary>The contract text of an RTP (<c>"0.97"</c>).</summary>
    public static string FormatRtp(decimal rtp) => rtp.ToString("0.00", CultureInfo.InvariantCulture);

    private static CurrentRound Current(Round round) => new(
        round.Id.Value,
        round.Number,
        round.State,
        round.Commitment,
        round.BettingEndsAt,
        round.StartedAt,
        round.CrashPoint is { } crashPoint ? new ContractMultiplier(crashPoint) : null,
        round.CrashedAt,
        round.RevealedSeed);
}
