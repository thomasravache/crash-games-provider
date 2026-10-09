using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Apogeu.BuildingBlocks.Contracts.Json;

/// <summary>
/// JSON conventions of the contracts (SPEC-0006 §6): web defaults (camelCase), <see cref="Money"/> and
/// <see cref="Multiplier"/> as decimal strings, <see cref="DateTimeOffset"/> as ISO-8601 UTC, enums as camelCase strings and
/// numbers only as JSON numbers (strict, so the schemas say <c>integer</c>/<c>number</c> rather than "number or string").
/// </summary>
public static class ContractJson
{
    private static readonly JsonSerializerOptions ReadOnlyOptions = CreateOptions();

    /// <summary>Ready-to-use, read-only options with every contract convention applied.</summary>
    public static JsonSerializerOptions Options => ReadOnlyOptions;

    /// <summary>Applies the contract conventions to existing options (for example, ASP.NET Core's HTTP JSON options).</summary>
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Converters.Add(new MoneyJsonConverter());
        options.Converters.Add(new MultiplierJsonConverter());
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        Configure(options);
        options.MakeReadOnly();
        return options;
    }
}
