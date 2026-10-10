using Apogeu.Wallet.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Apogeu.Wallet.Features;

/// <summary>
/// Applies the schema <c>wallet</c> migrations (with the insert-only triggers of the ledger) when the host starts in
/// Development (local Aspire environment). In any other environment the deployment pipeline applies them before the
/// release (ADR-0010), so this does nothing.
/// </summary>
internal sealed class WalletDevelopmentMigrations(IServiceProvider services, IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        environment.IsDevelopment() ? services.MigrateWalletDatabaseAsync(cancellationToken) : Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
