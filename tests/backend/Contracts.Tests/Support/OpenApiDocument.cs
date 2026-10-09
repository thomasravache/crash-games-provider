using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>Read-only navigation over an OpenAPI 3.x document (paths, responses, <c>$ref</c>, nullability).</summary>
internal sealed class OpenApiDocument(JsonElement root)
{
    public JsonElement Root { get; } = root;

    public static OpenApiDocument Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new OpenApiDocument(document.RootElement.Clone());
    }

    /// <summary>Schema of the <c>application/json</c> response of an operation, failing the test with context when absent.</summary>
    public JsonElement ResponseSchema(string path, string method, string status)
    {
        if (!Root.TryGetProperty("paths", out var paths) || !paths.TryGetProperty(path, out var item))
        {
            Assert.Fail($"The OpenAPI document has no path '{path}'. Paths: {DescribeKeys(Root, "paths")}");
            throw new UnreachableException();
        }

        if (!item.TryGetProperty(method, out var operation)
            || !operation.TryGetProperty("responses", out var responses)
            || !responses.TryGetProperty(status, out var response)
            || !response.TryGetProperty("content", out var content)
            || !content.TryGetProperty("application/json", out var media)
            || !media.TryGetProperty("schema", out var schema))
        {
            Assert.Fail($"The OpenAPI document has no '{status}' application/json response schema for {method.ToUpperInvariant()} {path}.");
            throw new UnreachableException();
        }

        return schema;
    }

    /// <summary>Follows local <c>$ref</c>s (<c>#/components/schemas/X</c>) until a concrete schema.</summary>
    public JsonElement Resolve(JsonElement schema)
    {
        var current = schema;
        for (var depth = 0; depth < 32; depth++)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty("$ref", out var reference))
            {
                return current;
            }

            current = ResolvePointer(reference.GetString() ?? string.Empty);
        }

        throw new InvalidOperationException("Too many nested $ref.");
    }

    /// <summary>Schema of a property of an object schema (after <c>$ref</c>), failing the test when absent.</summary>
    public JsonElement Property(JsonElement objectSchema, string name)
    {
        var resolved = Resolve(objectSchema);
        if (!resolved.TryGetProperty("properties", out var properties) || !properties.TryGetProperty(name, out var property))
        {
            Assert.Fail($"Schema has no property '{name}'. Schema: {resolved.GetRawText()}");
            throw new UnreachableException();
        }

        return property;
    }

    /// <summary>JSON Schema type names of a schema: <c>type</c> as string or array (OpenAPI 3.1), after <c>$ref</c>.</summary>
    public IReadOnlySet<string> TypeNames(JsonElement schema)
    {
        var resolved = Resolve(schema);
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (resolved.ValueKind == JsonValueKind.Object && resolved.TryGetProperty("type", out var type))
        {
            if (type.ValueKind == JsonValueKind.String)
            {
                names.Add(type.GetString()!);
            }
            else if (type.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in type.EnumerateArray())
                {
                    names.Add(item.GetString()!);
                }
            }
        }

        return names;
    }

    /// <summary>Whether the schema admits <c>null</c>: type array with "null" (3.1), <c>nullable: true</c> (3.0) or a null branch.</summary>
    public bool IsNullable(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (schema.TryGetProperty("nullable", out var nullable) && nullable.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (TypeNames(schema).Contains("null"))
        {
            return true;
        }

        foreach (var keyword in (string[])["oneOf", "anyOf"])
        {
            if (schema.TryGetProperty(keyword, out var branches) && branches.EnumerateArray().Any(IsNullable))
            {
                return true;
            }
        }

        return schema.TryGetProperty("$ref", out _) && IsNullable(Resolve(schema));
    }

    private JsonElement ResolvePointer(string reference)
    {
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Only local $ref are supported: '{reference}'.");
        }

        var current = Root;
        foreach (var segment in reference[2..].Split('/'))
        {
            var name = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (!current.TryGetProperty(name, out current))
            {
                Assert.Fail($"Unresolvable $ref '{reference}' in the OpenAPI document.");
            }
        }

        return current;
    }

    private static string DescribeKeys(JsonElement element, string property) =>
        element.TryGetProperty(property, out var child) && child.ValueKind == JsonValueKind.Object
            ? string.Join(", ", child.EnumerateObject().Select(p => p.Name))
            : "(none)";
}
