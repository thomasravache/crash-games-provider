using System.Text.Json;
using System.Text.RegularExpressions;

namespace Apogeu.Fairness.Tests.Support;

/// <summary>
/// Validator of the JSON Schema keywords used by tests/vectors/fairness-v1.schema.json (type, const, required, properties,
/// additionalProperties, items, minItems, minLength, pattern). No JSON Schema package is registered (ADR-0016); an unknown
/// keyword fails loudly instead of being ignored, so the schema cannot silently grow beyond what is checked.
/// </summary>
internal static class JsonSchemaSubset
{
    private static readonly HashSet<string> KnownKeywords = new(StringComparer.Ordinal)
    {
        "$schema", "$comment", "title", "description",
        "type", "const", "required", "properties", "additionalProperties", "items", "minItems", "minLength", "pattern",
    };

    /// <summary>The validation errors of <paramref name="instance"/> against <paramref name="schema"/> (empty when valid).</summary>
    public static IReadOnlyList<string> Validate(JsonElement instance, JsonElement schema)
    {
        var errors = new List<string>();
        Validate(instance, schema, "$", errors);
        return errors;
    }

    private static void Validate(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        foreach (var keyword in schema.EnumerateObject())
        {
            if (!KnownKeywords.Contains(keyword.Name))
            {
                throw new NotSupportedException($"JSON Schema keyword '{keyword.Name}' at {path} is not supported by the test validator.");
            }
        }

        if (schema.TryGetProperty("type", out var type) && !HasType(instance, type.GetString()!))
        {
            errors.Add($"{path}: expected {type.GetString()}, found {instance.ValueKind}");
            return;
        }

        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(instance, constant))
        {
            errors.Add($"{path}: expected the constant {constant.GetRawText()}");
        }

        switch (instance.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(instance, schema, path, errors);
                break;
            case JsonValueKind.Array:
                ValidateArray(instance, schema, path, errors);
                break;
            case JsonValueKind.String:
                ValidateString(instance.GetString()!, schema, path, errors);
                break;
        }
    }

    private static void ValidateObject(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var name in required.EnumerateArray().Select(item => item.GetString()!))
            {
                if (!instance.TryGetProperty(name, out _))
                {
                    errors.Add($"{path}: missing required property '{name}'");
                }
            }
        }

        var hasProperties = schema.TryGetProperty("properties", out var properties);
        var closed = schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False;
        foreach (var property in instance.EnumerateObject())
        {
            if (hasProperties && properties.TryGetProperty(property.Name, out var propertySchema))
            {
                Validate(property.Value, propertySchema, $"{path}.{property.Name}", errors);
            }
            else if (closed)
            {
                errors.Add($"{path}: property '{property.Name}' is not allowed");
            }
        }
    }

    private static void ValidateArray(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        if (schema.TryGetProperty("minItems", out var minItems) && instance.GetArrayLength() < minItems.GetInt32())
        {
            errors.Add($"{path}: expected at least {minItems.GetInt32()} items, found {instance.GetArrayLength()}");
        }

        if (schema.TryGetProperty("items", out var items))
        {
            var index = 0;
            foreach (var item in instance.EnumerateArray())
            {
                Validate(item, items, $"{path}[{index}]", errors);
                index++;
            }
        }
    }

    private static void ValidateString(string value, JsonElement schema, string path, List<string> errors)
    {
        if (schema.TryGetProperty("minLength", out var minLength) && value.Length < minLength.GetInt32())
        {
            errors.Add($"{path}: shorter than {minLength.GetInt32()} characters");
        }

        if (schema.TryGetProperty("pattern", out var pattern)
            && !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            errors.Add($"{path}: '{value}' does not match {pattern.GetString()}");
        }
    }

    private static bool HasType(JsonElement instance, string type) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
        "number" => instance.ValueKind == JsonValueKind.Number,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        _ => throw new NotSupportedException($"JSON Schema type '{type}' is not supported by the test validator."),
    };
}
