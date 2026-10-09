using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Threading.Channels;
using Apogeu.BuildingBlocks.Contracts.Json;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.HubContracts;

/// <summary>
/// JSON manifest of the hubs: per hub, the server methods (callable by the client) and the client methods (called by
/// the server through <c>TClient</c>), with parameter and return types as JSON Schemas under <see cref="ContractJson"/>.
/// </summary>
internal static class HubManifest
{
    private static readonly JsonSerializerOptions ManifestOptions = new() { WriteIndented = true };

    private static readonly JsonSchemaExporterOptions ExporterOptions = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = DescribeScalars,
    };

    private static readonly NullabilityInfoContext Nullability = new();

    /// <summary>Generic types a hub method result travels in: the result itself is their type argument.</summary>
    private static readonly HashSet<Type> Wrappers =
    [
        typeof(Task<>), typeof(ValueTask<>), typeof(IAsyncEnumerable<>), typeof(ChannelReader<>),
    ];

    private static readonly HashSet<string> LifecycleMethods = new(StringComparer.Ordinal)
    {
        nameof(Hub.OnConnectedAsync),
        nameof(Hub.OnDisconnectedAsync),
        nameof(Hub.Dispose),
    };

    public static string Build(IReadOnlyList<Type> hubs)
    {
        var manifest = new JsonObject
        {
            ["hubs"] = new JsonArray([.. hubs.Select(Describe)]),
        };
        return manifest.ToJsonString(ManifestOptions) + "\n";
    }

    private static JsonObject Describe(Type hub)
    {
        var client = HubDiscovery.ClientInterface(hub)!;
        var serverMethods = hub
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.DeclaringType != typeof(object)
                && !method.IsSpecialName
                && !LifecycleMethods.Contains(method.Name)
                && method.GetBaseDefinition().DeclaringType is { } declaring
                && !IsSignalRBase(declaring));
        var clientMethods = new[] { client }
            .Concat(client.GetInterfaces())
            .SelectMany(type => type.GetMethods());

        return new JsonObject
        {
            ["name"] = hub.Name,
            ["server"] = new JsonArray([.. serverMethods.OrderBy(m => m.Name, StringComparer.Ordinal).Select(DescribeMethod)]),
            ["client"] = new JsonArray([.. clientMethods.OrderBy(m => m.Name, StringComparer.Ordinal).Select(DescribeMethod)]),
        };
    }

    private static bool IsSignalRBase(Type type) =>
        type == typeof(Hub) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Hub<>));

    private static JsonObject DescribeMethod(MethodInfo method)
    {
        var (result, stream) = Unwrap(method.ReturnType);
        var parameters = method
            .GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .Select(parameter => new JsonObject
            {
                ["name"] = parameter.Name,
                ["schema"] = Schema(parameter.ParameterType, IsNullable(parameter)),
            });

        return new JsonObject
        {
            ["name"] = method.Name,
            ["parameters"] = new JsonArray([.. parameters]),
            ["returns"] = result is null ? null : Schema(result, IsNullableResult(method.ReturnParameter)),
            ["stream"] = stream,
        };
    }

    /// <summary>Result type of a hub method (<see langword="null"/> when none) and whether it is a server-to-client stream.</summary>
    private static (Type? Result, bool Stream) Unwrap(Type type)
    {
        if (type == typeof(void) || type == typeof(Task) || type == typeof(ValueTask))
        {
            return (null, false);
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (Wrappers.Contains(definition))
            {
                var argument = type.GetGenericArguments()[0];
                return definition == typeof(Task<>) || definition == typeof(ValueTask<>) ? Unwrap(argument) : (argument, true);
            }
        }

        return (type, false);
    }

    /// <summary>JSON Schema of <paramref name="type"/>; a nullable top-level reference type also admits <c>null</c>.</summary>
    private static JsonNode Schema(Type type, bool nullable)
    {
        var schema = ContractJson.Options.GetJsonSchemaAsNode(type, ExporterOptions);
        return nullable && !type.IsValueType
            ? new JsonObject { ["anyOf"] = new JsonArray(schema, new JsonObject { ["type"] = "null" }) }
            : schema;
    }

    private static bool IsNullable(ParameterInfo parameter) =>
        Nullability.Create(parameter).ReadState == NullabilityState.Nullable;

    /// <summary>Nullability of the result of a method, looking through <c>Task&lt;T&gt;</c>, <c>ValueTask&lt;T&gt;</c> and streams.</summary>
    private static bool IsNullableResult(ParameterInfo returnParameter)
    {
        var info = Nullability.Create(returnParameter);
        while (info.Type.IsGenericType && Wrappers.Contains(info.Type.GetGenericTypeDefinition()))
        {
            info = info.GenericTypeArguments[0];
        }

        return info.ReadState == NullabilityState.Nullable;
    }

    private static JsonNode DescribeScalars(JsonSchemaExporterContext context, JsonNode schema)
    {
        if (ContractScalars.Describe(context.TypeInfo.Type) is not { } scalar)
        {
            return schema;
        }

        var described = new JsonObject
        {
            ["type"] = scalar.IsNullable ? new JsonArray("string", "null") : "string",
            ["format"] = scalar.Format,
        };
        if (scalar.Pattern is not null)
        {
            described["pattern"] = scalar.Pattern;
        }

        return described;
    }
}
