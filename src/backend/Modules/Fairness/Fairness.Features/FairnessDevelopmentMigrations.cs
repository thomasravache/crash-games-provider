using Apogeu.Fairness.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Fairness.Features;

/// <summary>
/// Applies the schema <c>fairness</c> migrations when the host starts with <c>Database:MigrateOnStartup=true</c> (the
/// production compose, SPEC-0065) or, when the setting is absent, in Development (local Aspire environment). Otherwise the
/// deployment applies them before the release (ADR-0010), so this does nothing.
/// </summary>
internal sealed class FairnessDevelopmentMigrations(IServiceProvider services, IHostEnvironment environment, IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        MigrateOnStartup() ? services.MigrateFairnessDatabaseAsync(cancellationToken) : Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool MigrateOnStartup() =>
        bool.TryParse(configuration["Database:MigrateOnStartup"], out var migrate) ? migrate : environment.IsDevelopment();
}
