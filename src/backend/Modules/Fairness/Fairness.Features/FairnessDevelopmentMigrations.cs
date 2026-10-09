using Apogeu.Fairness.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Fairness.Features;

/// <summary>
/// Applies the schema <c>fairness</c> migrations when the host starts in Development (local Aspire environment). In any
/// other environment the deployment pipeline applies them before the release (ADR-0010), so this does nothing.
/// </summary>
internal sealed class FairnessDevelopmentMigrations(IServiceProvider services, IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        environment.IsDevelopment() ? services.MigrateFairnessDatabaseAsync(cancellationToken) : Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
