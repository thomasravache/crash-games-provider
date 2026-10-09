using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>
/// Adds a GET endpoint returning <see cref="ContractFixtureDto"/> to the real Host, so the fixture type goes through the
/// Host's own OpenAPI pipeline (schema transformers, JSON options) without existing in production code.
/// </summary>
internal sealed class FixtureEndpointStartupFilter : IStartupFilter
{
    public const string Route = "/__tests/contracts/fixture";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapGet(Route, () => TypedResults.Ok(ContractFixtureDto.Sample)));
    };
}
