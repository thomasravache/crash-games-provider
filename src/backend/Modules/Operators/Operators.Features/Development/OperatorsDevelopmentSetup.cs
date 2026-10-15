using Apogeu.Operators.Features.Catalog;
using Apogeu.Operators.Features.Configuration;
using Apogeu.Operators.Features.Observability;
using Apogeu.Operators.Infrastructure;
using Apogeu.Operators.Infrastructure.Catalog;
using Apogeu.Operators.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apogeu.Operators.Features.Development;

/// <summary>
/// Applies the schema <c>operators</c> migrations when <c>Database:MigrateOnStartup=true</c>, and ensures the
/// <see cref="DemoOperator"/> in its sala and then the <see cref="CatalogSeed"/> (the Pipa enabled for it and its BRL limits,
/// SPEC-0020) when <c>Operators:Demo:Enabled=true</c> — both on in the production compose (SPEC-0065). When a setting is
/// absent it follows the environment: on in Development (local Aspire environment), off elsewhere, where the deployment
/// applies the migrations (ADR-0010) and there is no seed. Both steps are idempotent.
/// </summary>
internal sealed class OperatorsDevelopmentSetup(
    IServiceProvider services,
    IHostEnvironment environment,
    IConfiguration configuration,
    ILogger<OperatorsDevelopmentSetup> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Enabled("Database:MigrateOnStartup"))
        {
            await services.MigrateOperatorsDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!Enabled("Operators:Demo:Enabled"))
        {
            return;
        }

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var directory = scope.ServiceProvider.GetRequiredService<IOperatorDirectory>();
            await directory.EnsureAsync(
                new OperatorRecord { Id = DemoOperator.Id, Name = DemoOperator.Name, SalaId = DemoOperator.SalaId },
                cancellationToken).ConfigureAwait(false);
        }

        OperatorsLog.DemoOperatorSeeded(logger, DemoOperator.Id, DemoOperator.SalaId);

        await CatalogSeed.SeedAsync(services.GetRequiredService<ICatalogStore>(), cancellationToken).ConfigureAwait(false);
        OperatorsLog.CatalogSeeded(logger, DemoOperator.Id, CatalogSeed.Pipa.Code.Value);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>The boolean setting at <paramref name="key"/>, or whether the host runs in Development when it is absent.</summary>
    private bool Enabled(string key) =>
        bool.TryParse(configuration[key], out var enabled) ? enabled : environment.IsDevelopment();
}
