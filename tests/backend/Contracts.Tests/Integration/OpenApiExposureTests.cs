using System.Net;
using Apogeu.Contracts.Tests.Support;
using Xunit;

namespace Apogeu.Contracts.Tests.Integration;

public sealed class OpenApiExposureTests
{
    [Theory(DisplayName = "Dado o Host num ambiente, quando GET /openapi/v1.json, então o documento só é publicado em Development")]
    [Trait("Spec", "SPEC-0006:IT-03")]
    [InlineData("Production", HttpStatusCode.NotFound)]
    [InlineData("Development", HttpStatusCode.OK)]
    public async Task GivenHostEnvironmentWhenOpenApiDocumentIsRequestedThenItIsPublishedOnlyInDevelopment(
        string environment,
        HttpStatusCode expected)
    {
        using var factory = ContractHost.Create(environment);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri(ContractHost.OpenApiDocumentPath, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }
}
