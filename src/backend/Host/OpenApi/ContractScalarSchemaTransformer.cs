using Apogeu.BuildingBlocks.Contracts.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Apogeu.Host.OpenApi;

/// <summary>
/// Describes the contract scalars (ADR-0008) as what travels on the wire: their custom JSON converters hide the shape
/// from the schema exporter, so <see cref="ContractScalars"/> sets it — a string with <c>format</c> <c>money</c>,
/// <c>multiplier</c> (branded <c>Money</c>/<c>Multiplier</c> in TypeScript) or <c>date-time</c>.
/// </summary>
internal sealed class ContractScalarSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (ContractScalars.Describe(context.JsonTypeInfo.Type) is not { } scalar)
        {
            return Task.CompletedTask;
        }

        schema.Type = scalar.IsNullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
        schema.Format = scalar.Format;
        schema.Pattern = scalar.Pattern;
        schema.Properties?.Clear();
        schema.Required?.Clear();
        return Task.CompletedTask;
    }
}
