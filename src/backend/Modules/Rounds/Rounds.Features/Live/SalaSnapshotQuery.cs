using System.Globalization;
using Apogeu.Rounds.Domain;
using Apogeu.Rounds.Features.Conductor;
using Apogeu.Rounds.Features.Salas;
using Microsoft.Extensions.DependencyInjection;
using ContractMultiplier = Apogeu.BuildingBlocks.Contracts.Multiplier;

namespace Apogeu.Rounds.Features.Live;

/// <summary>
/// The snapshot of a room for <c>JoinSala</c> (SPEC-0008 §6): its public configuration, the current round and the last 20
/// crashed rounds. The current round carries its crash point and seed only once it crashed: neither the store nor the
/// conductor's published state has them before.
/// </summary>
/// <remarks>
/// IMP-02: no join reads the database by itself. On the instance that leads the room, the snapshot comes from the state its
/// conductor published (<see cref="SalaLiveViews"/>); elsewhere, from a store read shared by every join waiting for it
/// (<see cref="CoalescedSalaRead"/>).
/// </remarks>
public sealed class SalaSnapshotQuery
{
    public const int HistorySize = 20;

    private readonly SalaCatalog _salas;
    private readonly SalaLiveViews _views;
    private readonly Dictionary<string, (SalaInfo Info, CoalescedSalaRead Read)> _rooms;

    internal SalaSnapshotQuery(SalaCatalog salas, SalaLiveViews views, IServiceScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(salas);
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(scopes);

        _salas = salas;
        _views = views;
        _rooms = salas.All.ToDictionary(
            sala => sala.Id,
            sala => (Info(sala), new CoalescedSalaRead(ct => ReadAsync(scopes, sala.Id, ct))),
            StringComparer.Ordinal);
    }

    /// <summary>Whether the room is configured.</summary>
    public bool Exists(string? salaId) => _salas.TryGet(salaId, out _);

    /// <summary>The snapshot of the room, or <see langword="null"/> when the room does not exist.</summary>
    public async Task<RoundSnapshot?> GetAsync(string salaId, CancellationToken ct)
    {
        if (salaId is null || !_rooms.TryGetValue(salaId, out var room))
        {
            return null;
        }

        if (!_views.TryGet(salaId, out var state))
        {
            state = await room.Read.ReadAsync(ct).ConfigureAwait(false);
        }

        return new RoundSnapshot(room.Info, state.Current, state.History);
    }

    /// <summary>The public configuration of the room: RTP with 2 decimals, the cap and the growth constant as text.</summary>
    public static SalaInfo Info(SalaConfig sala) => new(
        sala.Id,
        FormatRtp(sala.Rtp),
        new ContractMultiplier(sala.Cap),
        SalaConductor.FormatGrowthK(sala.GrowthK));

    /// <summary>The contract text of an RTP (<c>"0.97"</c>).</summary>
    public static string FormatRtp(decimal rtp) => rtp.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>The current round as sent to the clients: crash point and seed only once it crashed.</summary>
    internal static CurrentRound Current(Round round) => new(
        round.Id.Value,
        round.Number,
        round.State,
        round.Commitment,
        round.BettingEndsAt,
        round.StartedAt,
        round.CrashPoint is { } crashPoint ? new ContractMultiplier(crashPoint) : null,
        round.CrashedAt,
        round.RevealedSeed);

    /// <summary>A crashed round of the history.</summary>
    internal static RoundSummary Summary(Round round) =>
        new(round.Id.Value, round.Number, new ContractMultiplier(round.CrashPoint!.Value));

    private static async Task<SalaLiveViews.LiveState> ReadAsync(IServiceScopeFactory scopes, string salaId, CancellationToken ct)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var store = scope.ServiceProvider.GetRequiredService<IRoundStore>();
            var current = await store.FindLatestAsync(salaId, ct).ConfigureAwait(false);
            var history = await store.ListCrashedAsync(salaId, HistorySize, ct).ConfigureAwait(false);
            return new SalaLiveViews.LiveState(current is null ? null : Current(current), history.Select(Summary).ToArray());
        }
    }
}
