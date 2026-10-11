using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Betting.Features.Messaging;

/// <summary>
/// Known violation (ADR-0028, MessagingOwnedByHost): a module resolves the host's hosted services
/// (<c>IEnumerable&lt;IHostedService&gt;</c>) to drive their lifecycle itself.
/// </summary>
public static class HostedServiceLookup
{
    public static async Task StopOthersAsync(IServiceProvider provider, CancellationToken ct)
    {
        foreach (var hosted in provider.GetServices<IHostedService>())
        {
            await hosted.StopAsync(ct).ConfigureAwait(false);
        }
    }

    public static int CountRequired(IServiceProvider provider) =>
        provider.GetRequiredService<IEnumerable<IHostedService>>().Count();
}
