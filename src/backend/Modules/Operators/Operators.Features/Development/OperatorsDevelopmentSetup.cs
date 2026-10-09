using Apogeu.Operators.Features.Catalog;
using Apogeu.Operators.Features.Configuration;
using Apogeu.Operators.Features.Observability;
using Apogeu.Operators.Infrastructure;
using Apogeu.Operators.Infrastructure.Catalog;
using Apogeu.Operators.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apogeu.Operators.Features.Development;

/// <summary>
/// In Development only (local Aspire environment): applies the schema <c>operators</c> migrations, seeds the
/// <see cref="DemoOperator"/> in its sala and then the <see cref="CatalogSeed"/> (the Pipa enabled for it and its BRL limits,
/// SPEC-0020). Elsewhere the deployment pipeline applies the migrations (ADR-0010) and there is no seed.
/// </summary>
internal sealed class OperatorsDevelopmentSetup(IServiceProvider services, IHostEnvironment environment, ILogger<OperatorsDevelopmentSetup> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        await services.MigrateOperatorsDatabaseAsync(cancellationToken).ConfigureAwait(false);

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
}
