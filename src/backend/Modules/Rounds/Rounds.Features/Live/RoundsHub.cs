#pragma warning disable MA0025 // Scaffold of SPEC-0008 (no logic yet): implemented in the green phase.

using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Rounds.Features.Live;

/// <summary>
/// Hub <c>/hubs/rounds</c> (SPEC-0008 §6, ADR-0005): only translates messages; requires an authenticated user. Errors are
/// <see cref="HubException"/> whose message is the code (e.g. <c>SALA_NOT_FOUND</c>).
/// </summary>
public sealed class RoundsHub : Hub<IRoundsClient>
{
    public const string Path = "/hubs/rounds";

    /// <summary>Joins the room group and returns its snapshot; unknown room → <c>HubException("SALA_NOT_FOUND")</c>.</summary>
    public Task<RoundSnapshot> JoinSala(string salaId) => throw new NotImplementedException();

    /// <summary>The server time, for the client clock offset.</summary>
    public ServerTimeResponse GetServerTime() => throw new NotImplementedException();
}
