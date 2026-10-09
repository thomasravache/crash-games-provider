using System.Globalization;
using Apogeu.BuildingBlocks.Security;
using Apogeu.Operators.Features.Sessions;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Operators.Features.Hubs;

/// <summary>
/// Hub policy of <c>/hubs/rounds</c> (SPEC-0009 §6): <c>JoinSala(salaId)</c> for a sala other than the session's
/// <c>sala</c> claim fails with <c>HubException("FORBIDDEN")</c>. Registered for every hub of the host.
/// </summary>
internal sealed class SalaAccessHubFilter : IHubFilter
{
    public const string JoinSalaMethod = "JoinSala";

    public ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        if (string.Equals(invocationContext.HubMethodName, JoinSalaMethod, StringComparison.Ordinal))
        {
            var requested = invocationContext.HubMethodArguments.Count > 0
                ? Convert.ToString(invocationContext.HubMethodArguments[0], CultureInfo.InvariantCulture)
                : null;
            var allowed = invocationContext.Context.User?.FindFirst(PlayerSessionClaims.Sala)?.Value;
            if (requested is null || allowed is null || !string.Equals(requested, allowed, StringComparison.Ordinal))
            {
                throw new HubException(SecurityErrorCodes.Forbidden);
            }
        }

        return next(invocationContext);
    }
}
