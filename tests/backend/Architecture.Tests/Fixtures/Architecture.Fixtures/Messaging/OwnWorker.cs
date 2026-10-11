using Microsoft.Extensions.Hosting;

namespace Apogeu.Betting.Features.Messaging;

/// <summary>The module's own background work (control).</summary>
public sealed class OwnWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
