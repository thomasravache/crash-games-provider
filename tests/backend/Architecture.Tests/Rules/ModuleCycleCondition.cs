using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Conditions;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace Apogeu.Architecture.Tests.Rules;

/// <summary>
/// ADR-0001: no dependency cycle between modules. Types are grouped by the <c>Apogeu.&lt;Module&gt;</c> namespace segment
/// into a module graph (every layer counts, Contracts included); a type fails when one of its dependencies on another
/// module leads back to its own module. The failure names the cycle and the dependency that closes it.
/// </summary>
internal sealed partial class ModuleCycleCondition : ICondition<IType>
{
    public string Description => "not take part in a dependency cycle between modules";

    public IEnumerable<ConditionResult> Check(IEnumerable<IType> objects, ArchitectureModel architecture)
    {
        var graph = BuildModuleGraph(architecture.Types);
        foreach (var type in objects)
        {
            var origin = ModuleOf(type);
            var cycles = origin is null
                ? []
                : TypeConditions.ReferencedTypes(type)
                    .Select(target => (Target: target, Module: ModuleOf(target)))
                    .Where(dependency => dependency.Module is not null
                        && !string.Equals(dependency.Module, origin, StringComparison.Ordinal))
                    .Select(dependency => (dependency.Target, Path: PathBetween(graph, dependency.Module!, origin)))
                    .Where(dependency => dependency.Path is not null)
                    .Select(dependency => $"{origin} -> {string.Join(" -> ", dependency.Path!)} (through {dependency.Target.FullName})")
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList();
            yield return new ConditionResult(type, cycles.Count == 0, "closes the module cycle " + string.Join("; ", cycles));
        }
    }

    public bool CheckEmpty() => true;

    private static Dictionary<string, HashSet<string>> BuildModuleGraph(IEnumerable<IType> types)
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            if (ModuleOf(type) is not { } origin)
            {
                continue;
            }

            var edges = graph.TryGetValue(origin, out var existing) ? existing : graph[origin] = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in TypeConditions.ReferencedTypes(type).Select(ModuleOf).OfType<string>())
            {
                if (!string.Equals(target, origin, StringComparison.Ordinal))
                {
                    edges.Add(target);
                }
            }
        }

        return graph;
    }

    /// <summary>Shortest module path from <paramref name="from"/> to <paramref name="to"/> (both included), or null.</summary>
    private static List<string>? PathBetween(Dictionary<string, HashSet<string>> graph, string from, string to)
    {
        var previous = new Dictionary<string, string>(StringComparer.Ordinal) { [from] = from };
        var queue = new Queue<string>([from]);
        while (queue.TryDequeue(out var current))
        {
            if (string.Equals(current, to, StringComparison.Ordinal))
            {
                var path = new List<string> { current };
                while (!string.Equals(current, from, StringComparison.Ordinal))
                {
                    current = previous[current];
                    path.Insert(0, current);
                }

                return path;
            }

            foreach (var next in graph.GetValueOrDefault(current) ?? [])
            {
                if (previous.TryAdd(next, current))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return null;
    }

    private static string? ModuleOf(IType type)
    {
        var match = ModulePattern().Match(type.Namespace.FullName);
        return match.Success ? match.Groups["module"].Value : null;
    }

    [GeneratedRegex(@"^Apogeu\.(?<module>[A-Za-z0-9]+)(\.|$)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ModulePattern();
}
