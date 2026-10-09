using Apogeu.BuildingBlocks.Contracts.Json;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Host.OpenApi;

/// <summary>
/// OpenAPI document of the Host (ADR-0008, SPEC-0006): generated from the endpoints with the contract JSON conventions,
/// at build time (artifacts) and, in Development only, served at <c>/openapi/v1.json</c>.
/// </summary>
internal static class OpenApiExtensions
{
    public const string DocumentName = "v1";

    /// <summary>
    /// Applies the contract JSON conventions to the HTTP APIs and to the SignalR JSON hub protocol, and registers the
    /// OpenAPI document.
    /// </summary>
    public static IServiceCollection AddContractOpenApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => ContractJson.Configure(options.SerializerOptions));
        services.Configure<JsonHubProtocolOptions>(options => ContractJson.Configure(options.PayloadSerializerOptions));
        services.AddOpenApi(DocumentName, options => options.AddSchemaTransformer<ContractScalarSchemaTransformer>());
        return services;
    }

    /// <summary>Publishes the document only in Development: no API documentation in other environments without a decision.</summary>
    public static WebApplication MapContractOpenApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        return app;
    }
}
