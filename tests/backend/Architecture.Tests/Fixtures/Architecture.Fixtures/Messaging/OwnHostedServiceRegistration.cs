using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Betting.Features.Messaging;

/// <summary>
/// Allowed (control of MessagingOwnedByHost): a module registers and implements its own hosted service, as the real
/// modules do for their Development migrations and the reservation sweeper.
/// </summary>
public static class OwnHostedServiceRegistration
{
    public static IServiceCollection AddOwnWorker(IServiceCollection services) => services.AddHostedService<OwnWorker>();
}
