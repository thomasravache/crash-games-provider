using Apogeu.BuildingBlocks.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Rounds.Features.Live;

/// <summary>
/// Hub <c>/hubs/rounds</c> (SPEC-0008 §6, ADR-0005): only translates messages; requires a player session (SPEC-0009), whose
/// <c>sala</c> claim the global hub filter of SPEC-0009 checks against the <c>salaId</c> of <c>JoinSala</c>. Errors are
/// <see cref="HubException"/> whose message is the code (e.g. <c>SALA_NOT_FOUND</c>).
/// </summary>
[Authorize(Policy = SecurityPolicies.PlayerSession)]
public sealed class RoundsHub : Hub<IRoundsClient>
{
    public const string Path = "/hubs/rounds";

    private readonly SalaSnapshotQuery _snapshots;
    private readonly TimeProvider _time;

    public RoundsHub(SalaSnapshotQuery snapshots, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(time);
        _snapshots = snapshots;
        _time = time;
    }

    /// <summary>The SignalR group of the clients of a room.</summary>
    public static string GroupOf(string salaId) => "sala:" + salaId;

    /// <summary>Joins the room group and returns its snapshot; unknown room → <c>HubException("SALA_NOT_FOUND")</c>.</summary>
    public async Task<RoundSnapshot> JoinSala(string salaId)
    {
        if (!_snapshots.Exists(salaId))
        {
            throw new HubException(RoundsErrorCodes.SalaNotFound);
        }

        // Join first, so no message sent while the snapshot is read is missed.
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(salaId), Context.ConnectionAborted).ConfigureAwait(false);
        var snapshot = await _snapshots.GetAsync(salaId, Context.ConnectionAborted).ConfigureAwait(false)
            ?? throw new HubException(RoundsErrorCodes.SalaNotFound);

        return snapshot;
    }

    /// <summary>The server time, for the client clock offset.</summary>
    public ServerTimeResponse GetServerTime() => new(_time.GetUtcNow());
}
