using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Security.Tests.Support;

/// <summary>
/// Stand-in for the rounds hub of SPEC-0008 (developed in parallel), mapped at <c>/hubs/rounds</c> by the tests only.
/// It has no attribute and no check of its own: authentication (deny by default) and the sala policy must come from what
/// SPEC-0009 registers in the Host. When the real hub reaches this branch, IT-05 must target it and this class goes away.
/// </summary>
public sealed class RoundsHubStandIn : Hub
{
    public const string Path = "/hubs/rounds";

    /// <summary>Echoes the sala and the caller's <c>sala</c> claim, so a test can see the call went through.</summary>
    public string JoinSala(string salaId) => salaId + "|" + Context.User?.FindFirst("sala")?.Value;
}
