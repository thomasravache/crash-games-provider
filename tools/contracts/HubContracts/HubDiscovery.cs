using System.Reflection;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.HubContracts;

/// <summary>Finds the strongly typed SignalR hubs (<see cref="Hub{T}"/>) in the Apogeu assemblies next to this tool.</summary>
internal static class HubDiscovery
{
    private const string AssemblyPrefix = "Apogeu.";

    public static IReadOnlyList<Type> FindHubs(string directory) =>
        [.. Directory
            .EnumerateFiles(directory, AssemblyPrefix + "*.dll")
            .Order(StringComparer.Ordinal)
            .Select(path => Assembly.LoadFrom(path))
            .SelectMany(LoadableTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false } && ClientInterface(type) is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal)];

    /// <summary>The <c>TClient</c> of <c>Hub&lt;TClient&gt;</c> in the base chain of <paramref name="hub"/>, if any.</summary>
    public static Type? ClientInterface(Type hub)
    {
        for (var type = hub.BaseType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Hub<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
