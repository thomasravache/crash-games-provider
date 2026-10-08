using System.Net;
using System.Text.Json;
using Apogeu.Contracts.Tests.Support;
using Xunit;

namespace Apogeu.Contracts.Tests.Contract;

public sealed class MetaEndpointContractTests
{
    private const string MetaPath = "/api/v1/meta";

    [Fact(DisplayName = "Dado o Host em execução, quando GET /api/v1/meta, então a resposta valida contra o esquema do OpenAPI gerado")]
    [Trait("Spec", "SPEC-0006:CT-01")]
    public async Task GivenRunningHostWhenMetaIsRequestedThenResponseValidatesAgainstGeneratedOpenApiSchema()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = ContractHost.Create("Development");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(MetaPath, UriKind.Relative), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        using var documentResponse = await client.GetAsync(new Uri(ContractHost.OpenApiDocumentPath, UriKind.Relative), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        var openApi = OpenApiDocument.Parse(await documentResponse.Content.ReadAsStringAsync(cancellationToken));
        var schema = openApi.ResponseSchema(MetaPath, "get", "200");

        // The schema must describe the contract of §6, otherwise an empty schema would validate anything.
        Assert.Contains("string", openApi.TypeNames(openApi.Property(schema, "apiVersion")));
        Assert.Contains("string", openApi.TypeNames(openApi.Property(schema, "serverTime")));

        var errors = OpenApiSchemaValidator.Validate(openApi, schema, body.RootElement);
        Assert.True(
            errors.Count == 0,
            $"GET {MetaPath} body does not validate against the OpenAPI schema:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}"
            + $"{Environment.NewLine}Body: {body.RootElement.GetRawText()}");
        foreach (var property in (string[])["apiVersion", "serverTime"])
        {
            Assert.True(body.RootElement.TryGetProperty(property, out _), $"Expected '{property}' in the body (SPEC-0006 §6).");
        }
    }
}
