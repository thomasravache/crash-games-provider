using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Operators.Features.Hubs;

#pragma warning disable MA0025 // Scaffold of SPEC-0009 (no logic yet): implemented in the green phase.

/// <summary>
/// Hub policy of <c>/hubs/rounds</c> (SPEC-0009 §6): <c>JoinSala(salaId)</c> for a sala other than the session's
/// <c>sala</c> claim fails with <c>HubException("FORBIDDEN")</c>.
/// </summary>
internal sealed class SalaAccessHubFilter : IHubFilter
{
    public ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next) =>
        throw new NotImplementedException();
}
#pragma warning restore MA0025
