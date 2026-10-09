using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Apogeu.Contracts.Tests.Support;

/// <summary>
/// Structural validation of a JSON instance against an OpenAPI 3.x schema, with System.Text.Json only (no extra package):
/// <c>$ref</c>, <c>type</c> (string or array), <c>nullable</c>, <c>enum</c>, <c>required</c>, <c>properties</c>,
/// <c>additionalProperties</c>, <c>items</c>, <c>allOf</c>/<c>oneOf</c>/<c>anyOf</c>, <c>pattern</c> and the
/// <c>date-time</c> format. Returns the violations as JSON-pointer-prefixed messages.
/// </summary>
internal static class OpenApiSchemaValidator
{
    public static IReadOnlyList<string> Validate(OpenApiDocument document, JsonElement schema, JsonElement instance)
    {
        var errors = new List<string>();
        Validate(document, schema, instance, "$", errors);
        return errors;
    }

    private static void Validate(OpenApiDocument document, JsonElement schema, JsonElement instance, string at, List<string> errors)
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

        schema = document.Resolve(schema);

        if (instance.ValueKind == JsonValueKind.Null && document.IsNullable(schema))
        {
            return;
        }

        ValidateType(document, schema, instance, at, errors);
        ValidateEnum(schema, instance, at, errors);
        ValidateComposition(document, schema, instance, at, errors);

        switch (instance.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(document, schema, instance, at, errors);
                break;
            case JsonValueKind.Array when schema.TryGetProperty("items", out var items):
                var index = 0;
                foreach (var item in instance.EnumerateArray())
                {
                    Validate(document, items, item, string.Create(CultureInfo.InvariantCulture, $"{at}[{index++}]"), errors);
                }

                break;
            case JsonValueKind.String:
                ValidateString(schema, instance.GetString()!, at, errors);
                break;
        }
    }

    private static void ValidateType(OpenApiDocument document, JsonElement schema, JsonElement instance, string at, List<string> errors)
    {
        var types = document.TypeNames(schema);
        if (types.Count == 0 || types.Any(type => Matches(type, instance)))
        {
            return;
        }

        errors.Add($"{at}: expected type [{string.Join(", ", types)}] but got {instance.ValueKind} ({instance.GetRawText()})");
    }

    private static bool Matches(string type, JsonElement instance) => type switch
    {
        "null" => instance.ValueKind == JsonValueKind.Null,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "number" => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
        _ => false,
    };

    private static void ValidateEnum(JsonElement schema, JsonElement instance, string at, List<string> errors)
    {
        if (!schema.TryGetProperty("enum", out var values))
        {
            return;
        }

        var raw = instance.GetRawText();
        if (!values.EnumerateArray().Any(value => string.Equals(value.GetRawText(), raw, StringComparison.Ordinal)))
        {
            errors.Add($"{at}: {raw} is not one of {values.GetRawText()}");
        }
    }

    private static void ValidateComposition(OpenApiDocument document, JsonElement schema, JsonElement instance, string at, List<string> errors)
    {
        if (schema.TryGetProperty("allOf", out var allOf))
        {
            foreach (var part in allOf.EnumerateArray())
            {
                Validate(document, part, instance, at, errors);
            }
        }

        foreach (var keyword in (string[])["oneOf", "anyOf"])
        {
            if (!schema.TryGetProperty(keyword, out var branches))
            {
                continue;
            }

            var matching = branches.EnumerateArray().Count(branch => Validate(document, branch, instance).Count == 0);
            if (matching == 0 || (string.Equals(keyword, "oneOf", StringComparison.Ordinal) && matching > 1))
            {
                errors.Add($"{at}: {matching} branch(es) of {keyword} match the value {instance.GetRawText()}");
            }
        }
    }

    private static void ValidateObject(OpenApiDocument document, JsonElement schema, JsonElement instance, string at, List<string> errors)
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

        var properties = schema.TryGetProperty("properties", out var declared) ? declared : default;
        var additional = schema.TryGetProperty("additionalProperties", out var extra) ? extra : default;
        foreach (var property in instance.EnumerateObject())
        {
            if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out var propertySchema))
            {
                Validate(document, propertySchema, property.Value, $"{at}.{property.Name}", errors);
            }
            else if (additional.ValueKind is JsonValueKind.Object or JsonValueKind.False)
            {
                Validate(document, additional, property.Value, $"{at}.{property.Name}", errors);
            }
        }
    }

    private static void ValidateString(JsonElement schema, string value, string at, List<string> errors)
    {
        if (schema.TryGetProperty("format", out var format)
            && string.Equals(format.GetString(), "date-time", StringComparison.Ordinal)
            && !DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            errors.Add($"{at}: '{value}' is not an RFC 3339 date-time");
        }

        if (schema.TryGetProperty("pattern", out var pattern)
            && !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            errors.Add($"{at}: '{value}' does not match the pattern {pattern.GetString()}");
        }
    }
}
