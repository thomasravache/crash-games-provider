using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Betting.Features.Messaging;

/// <summary>
/// Known violation (ADR-0028, MessagingOwnedByHost): a module removes every hosted service of the host with
/// <c>RemoveAll&lt;IHostedService&gt;()</c>, without naming <see cref="ServiceDescriptor"/> itself.
/// </summary>
public static class HostedServiceRemoval
{
    public static IServiceCollection StopEveryHostedService(IServiceCollection services) => services.RemoveAll<IHostedService>();
}
