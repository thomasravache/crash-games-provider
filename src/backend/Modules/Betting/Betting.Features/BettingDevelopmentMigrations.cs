using Apogeu.Betting.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Betting.Features;

/// <summary>
/// Applies the schema <c>betting</c> migrations when the host starts in Development (local Aspire environment). In any other
/// environment the deployment pipeline applies them before the release (ADR-0010), so this does nothing.
/// </summary>
internal sealed class BettingDevelopmentMigrations(IServiceProvider services, IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        environment.IsDevelopment() ? services.MigrateBettingDatabaseAsync(cancellationToken) : Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
