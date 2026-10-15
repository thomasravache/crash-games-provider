using System.Text.Json;
using Apogeu.DemoCasino.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.DemoCasino.Tests.Contract;

// SPEC-0045 CT-01 (Emenda v1): the real launch request of the DemoCasino (the body captured as it left the casino's HTTP
// pipeline) conforms, strictly, to the LaunchRequest schema of the OpenAPI the Apogeu Host generates (SPEC-0006,
// SPEC-0009@1): POST of /api/v1/operator/launch, a JSON object with every required property, each of the declared type,
// and no property the schema does not declare.
[Collection(ApogeuDependencies.Name)]
public sealed class LaunchRequestContractTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const string LaunchPath = "/api/v1/operator/launch";

    [Fact(DisplayName = "Dada a requisição real de lançamento do DemoCasino, quando validada de forma estrita contra o LaunchRequest do OpenAPI gerado da Apogeu, então confere")]
    [Trait("Spec", "SPEC-0045:CT-01")]
    [Trait("Category", "Contract")]
    public async Task GivenRealDemoCasinoLaunchRequestWhenValidatedStrictlyAgainstGeneratedOpenApiThenItConforms()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var apogeu = await ApogeuApiHost.StartAsync(postgres.ConnectionString, redis.ConnectionString, ct);
        var openApi = await apogeu.OpenApiAsync(ct);
        using var capture = new CapturingHandler(CapturingHandler.LaunchResponse);
        await using var casino = LobbyCasino.Against(capture);
        using var browser = await CasinoApi.SignedInBrowserAsync(casino, ct);

        using var response = await CasinoApi.PlayAsync(browser, "pipa", ct);

        var launch = Assert.Single(capture.Requests, request => string.Equals(request.Uri.AbsolutePath, LaunchPath, StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Post, launch.Method);
        var schema = RequestSchema(openApi, LaunchPath, "post");
        using var body = JsonDocument.Parse(launch.Body);
        var errors = new List<string>();
        Validate(openApi, schema, body.RootElement, "$", errors);
        Assert.True(errors.Count == 0, "The launch body does not conform to LaunchRequest: " + string.Join("; ", errors) + ". Body: " + body.RootElement.GetRawText());
    }

    /// <summary>The JSON request schema of the operation, with its <c>$ref</c> resolved.</summary>
    private static JsonElement RequestSchema(JsonElement openApi, string path, string method)
    {
        Assert.True(openApi.GetProperty("paths").TryGetProperty(path, out var item), $"The OpenAPI has no path {path}.");
        Assert.True(item.TryGetProperty(method, out var operation), $"The OpenAPI has no {method} {path}.");
        var schema = operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var resolved = Resolve(openApi, schema);
        Assert.True(schema.TryGetProperty("$ref", out var reference) && string.Equals(reference.GetString(), "#/components/schemas/LaunchRequest", StringComparison.Ordinal), $"The request schema of {method} {path} must be LaunchRequest, got {schema.GetRawText()}.");
        return resolved;
    }

    private static JsonElement Resolve(JsonElement openApi, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
        {
            return schema;
        }

        var target = openApi;
        foreach (var segment in reference.GetString()!.TrimStart('#', '/').Split('/'))
        {
            target = target.GetProperty(segment);
        }

        return target;
    }

    /// <summary>A strict check: types, required properties and no undeclared property (whatever additionalProperties says).</summary>
    private static void Validate(JsonElement openApi, JsonElement schema, JsonElement value, string at, List<string> errors)
    {
        schema = Resolve(openApi, schema);
        if (!TypeMatches(schema, value))
        {
            errors.Add($"{at}: {value.ValueKind} is not of type {(schema.TryGetProperty("type", out var type) ? type.GetRawText() : "<any>")}");
            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var properties = schema.TryGetProperty("properties", out var declared) ? declared : default;
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var name in required.EnumerateArray().Select(item => item.GetString()!))
            {
                if (!value.TryGetProperty(name, out _))
                {
                    errors.Add($"{at}: required property '{name}' is missing");
                }
            }
        }

        foreach (var property in value.EnumerateObject())
        {
            if (properties.ValueKind != JsonValueKind.Object || !properties.TryGetProperty(property.Name, out var propertySchema))
            {
                errors.Add($"{at}: property '{property.Name}' is not declared by the schema");
                continue;
            }

            Validate(openApi, propertySchema, property.Value, $"{at}.{property.Name}", errors);
        }
    }

    private static bool TypeMatches(JsonElement schema, JsonElement value)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return true;
        }

        var types = type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(item => item.GetString()!).ToList()
            : [type.GetString()!];
        return types.Any(name => name switch
        {
            "null" => value.ValueKind == JsonValueKind.Null,
            "string" => value.ValueKind == JsonValueKind.String,
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            _ => false,
        });
    }
}
