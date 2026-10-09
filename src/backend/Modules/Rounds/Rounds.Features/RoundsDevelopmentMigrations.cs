using Apogeu.Rounds.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Rounds.Features;

/// <summary>
/// Applies the schema <c>rounds</c> migrations when the host starts in Development (local Aspire environment). In any other
/// environment the deployment pipeline applies them before the release (ADR-0010), so this does nothing.
/// </summary>
internal sealed class RoundsDevelopmentMigrations(IServiceProvider services, IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        environment.IsDevelopment() ? services.MigrateRoundsDatabaseAsync(cancellationToken) : Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
