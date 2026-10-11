using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Betting.Features.Messaging;

/// <summary>
/// Known violation (ADR-0028, MessagingOwnedByHost): a module edits the service registrations, removing the hosted
/// services of other assemblies through their <see cref="ServiceDescriptor"/>.
/// </summary>
public static class HostedServiceDescriptorEdit
{
    public static IServiceCollection RemoveForeignHostedServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
        {
            services.Remove(descriptor);
        }

        return services;
    }
}
