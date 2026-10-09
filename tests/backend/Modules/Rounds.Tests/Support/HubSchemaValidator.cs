using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// Validator of the JSON Schema keywords the hub-contract exporter (SPEC-0006, System.Text.Json schema exporter) writes:
/// type (string or array), enum, const, properties, required, additionalProperties, items, anyOf/oneOf, pattern, format
/// (date-time, uuid, money, multiplier) and local <c>$ref</c>. A property the schema does not declare is reported, so a
/// message cannot silently carry more than the contract. An unknown keyword fails loudly. No JSON Schema package (ADR-0016).
/// </summary>
internal static class HubSchemaValidator
{
    private static readonly HashSet<string> KnownKeywords = new(StringComparer.Ordinal)
    {
        "$schema", "$comment", "title", "description", "type", "enum", "const", "properties", "required",
        "additionalProperties", "items", "anyOf", "oneOf", "pattern", "format", "$ref",
    };

    public static IReadOnlyList<string> Validate(JsonElement schema, JsonElement instance)
    {
        var errors = new List<string>();
        Validate(schema, schema, instance, "$", errors);
        return errors;
    }

    private static void Validate(JsonElement root, JsonElement schema, JsonElement instance, string at, List<string> errors)
    {
        if (schema.ValueKind == JsonValueKind.True)
        {
            return;
        }

        if (schema.ValueKind == JsonValueKind.False)
        {
            errors.Add($"{at}: no value is allowed here");
            return;
        }

        foreach (var keyword in schema.EnumerateObject())
        {
            if (!KnownKeywords.Contains(keyword.Name))
            {
                throw new NotSupportedException($"JSON Schema keyword '{keyword.Name}' at {at} is not supported by the test validator.");
            }
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            Validate(root, Resolve(root, reference.GetString()!), instance, at, errors);
            return;
        }

        if (schema.TryGetProperty("type", out var type) && !TypeNames(type).Any(name => Matches(name, instance)))
        {
            errors.Add($"{at}: expected type [{string.Join(", ", TypeNames(type))}] but got {instance.ValueKind} ({instance.GetRawText()})");
            return;
        }

        ValidateValue(schema, instance, at, errors);
        ValidateComposition(root, schema, instance, at, errors);

        switch (instance.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(root, schema, instance, at, errors);
                break;
            case JsonValueKind.Array when schema.TryGetProperty("items", out var items):
                var index = 0;
                foreach (var item in instance.EnumerateArray())
                {
                    Validate(root, items, item, string.Create(CultureInfo.InvariantCulture, $"{at}[{index++}]"), errors);
                }

                break;
            case JsonValueKind.String:
                ValidateString(schema, instance.GetString()!, at, errors);
                break;
        }
    }

    private static void ValidateValue(JsonElement schema, JsonElement instance, string at, List<string> errors)
    {
        if (schema.TryGetProperty("enum", out var values) && !values.EnumerateArray().Any(v => JsonElement.DeepEquals(v, instance)))
        {
            errors.Add($"{at}: {instance.GetRawText()} is not one of {values.GetRawText()}");
        }

        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(constant, instance))
        {
            errors.Add($"{at}: expected the constant {constant.GetRawText()}");
        }
    }

    private static void ValidateComposition(JsonElement root, JsonElement schema, JsonElement instance, string at, List<string> errors)
    {
        foreach (var keyword in (string[])["anyOf", "oneOf"])
        {
            if (schema.TryGetProperty(keyword, out var branches))
            {
                var matching = branches.EnumerateArray().Count(branch =>
                {
                    var branchErrors = new List<string>();
                    Validate(root, branch, instance, at, branchErrors);
                    return branchErrors.Count == 0;
                });
                if (matching == 0 || (string.Equals(keyword, "oneOf", StringComparison.Ordinal) && matching > 1))
                {
                    errors.Add($"{at}: {matching} branch(es) of {keyword} match {instance.GetRawText()}");
                }
            }
        }
    }

    private static void ValidateObject(JsonElement root, JsonElement schema, JsonElement instance, string at, List<string> errors)
    {
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var name in required.EnumerateArray().Select(n => n.GetString()!))
            {
                if (!instance.TryGetProperty(name, out _))
                {
                    errors.Add($"{at}: required property '{name}' is missing");
                }
            }
        }

        var hasProperties = schema.TryGetProperty("properties", out var properties);
        var hasAdditional = schema.TryGetProperty("additionalProperties", out var additional);
        foreach (var property in instance.EnumerateObject())
        {
            if (hasProperties && properties.TryGetProperty(property.Name, out var propertySchema))
            {
                Validate(root, propertySchema, property.Value, $"{at}.{property.Name}", errors);
            }
            else if (hasAdditional && additional.ValueKind == JsonValueKind.Object)
            {
                Validate(root, additional, property.Value, $"{at}.{property.Name}", errors);
            }
            else
            {
                errors.Add($"{at}: property '{property.Name}' is not in the contract");
            }
        }
    }

    private static void ValidateString(JsonElement schema, string value, string at, List<string> errors)
    {
        if (schema.TryGetProperty("format", out var format))
        {
            var valid = format.GetString() switch
            {
                "date-time" => DateTimeOffset.TryParseExact(
                    value, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                "uuid" => Guid.TryParse(value, out _),
                _ => true,
            };
            if (!valid)
            {
                errors.Add($"{at}: '{value}' is not a valid {format.GetString()}");
            }
        }

        if (schema.TryGetProperty("pattern", out var pattern)
            && !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            errors.Add($"{at}: '{value}' does not match the pattern {pattern.GetString()}");
        }
    }

    private static IReadOnlyList<string> TypeNames(JsonElement type) =>
        type.ValueKind == JsonValueKind.Array ? [.. type.EnumerateArray().Select(t => t.GetString()!)] : [type.GetString()!];

    private static bool Matches(string type, JsonElement instance) => type switch
    {
        "null" => instance.ValueKind == JsonValueKind.Null,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "number" => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
        _ => throw new NotSupportedException($"JSON Schema type '{type}' is not supported by the test validator."),
    };

    private static JsonElement Resolve(JsonElement root, string reference)
    {
        if (!reference.StartsWith('#'))
        {
            throw new NotSupportedException($"Only local $ref are supported, got '{reference}'.");
        }

        var node = root;
        foreach (var segment in reference.TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            node = node.ValueKind == JsonValueKind.Array
                ? node[int.Parse(name, CultureInfo.InvariantCulture)]
                : node.GetProperty(name);
        }

        return node;
    }
}
