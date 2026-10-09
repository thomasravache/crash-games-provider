using Apogeu.Contracts.Tests.Support;
using Xunit;

namespace Apogeu.Contracts.Tests.Unit;

/// <summary>
/// Type mapping of the contract pipeline (C# DTO → OpenAPI → TypeScript) proven on the test-only
/// <see cref="ContractFixtureDto"/>, exposed through the real Host's OpenAPI pipeline.
/// </summary>
public sealed class ContractSchemaMappingTests(FixtureContractGeneration generation) : IClassFixture<FixtureContractGeneration>
{
    private const string Schema = nameof(ContractFixtureDto);
    private const string Stake = "fixtureStake";
    private const string Cashout = "fixtureCashout";
    private const string Nickname = "fixtureNickname";
    private const string Label = "fixtureLabel";

    [Fact(DisplayName = "Dado um DTO com Money e Multiplier, quando o esquema é gerado, então são string no OpenAPI e Money/Multiplier no TS")]
    [Trait("Spec", "SPEC-0006:UT-01")]
    public async Task GivenDtoWithMoneyAndMultiplierWhenSchemaIsGeneratedThenStringInOpenApiAndBrandedTypesInTypeScript()
    {
        var openApi = await generation.OpenApiAsync();
        var schema = FixtureSchema(openApi);

        foreach (var property in (string[])[Stake, Cashout])
        {
            var types = openApi.TypeNames(openApi.Property(schema, property));
            Assert.True(
                types.SetEquals(["string"]),
                $"'{property}' must be a non-nullable string in the OpenAPI document, got [{string.Join(", ", types)}].");
        }

        var typeScript = await generation.TypeScriptAsync();
        Assert.Equal("Money", TypeScriptGenerator.PropertyType(typeScript, Stake));
        Assert.Equal("Multiplier", TypeScriptGenerator.PropertyType(typeScript, Cashout));
    }

    [Fact(DisplayName = "Dado um DTO com string?, quando o esquema é gerado, então a propriedade é anulável no OpenAPI e string | null no TS")]
    [Trait("Spec", "SPEC-0006:UT-02")]
    public async Task GivenDtoWithNullableStringWhenSchemaIsGeneratedThenNullableInOpenApiAndStringOrNullInTypeScript()
    {
        var openApi = await generation.OpenApiAsync();
        var schema = FixtureSchema(openApi);

        var nickname = openApi.Property(schema, Nickname);
        Assert.True(openApi.IsNullable(nickname), $"'{Nickname}' (string?) must be nullable in the OpenAPI document: {nickname.GetRawText()}");
        Assert.Contains("string", openApi.TypeNames(nickname));
        var label = openApi.Property(schema, Label);
        Assert.False(openApi.IsNullable(label), $"Control: '{Label}' (string) must not be nullable: {label.GetRawText()}");

        var typeScript = await generation.TypeScriptAsync();
        Assert.Contains(TypeScriptGenerator.PropertyType(typeScript, Nickname), (string[])["string | null", "null | string"], StringComparer.Ordinal);
        Assert.Equal("string", TypeScriptGenerator.PropertyType(typeScript, Label));
    }

    private static System.Text.Json.JsonElement FixtureSchema(OpenApiDocument openApi)
    {
        var schema = openApi.Resolve(openApi.ResponseSchema(FixtureEndpointStartupFilter.Route, "get", "200"));
        Assert.True(
            openApi.TypeNames(schema).Contains("object") || schema.TryGetProperty("properties", out _),
            $"Expected the {Schema} response to be an object schema: {schema.GetRawText()}");
        return schema;
    }
}
