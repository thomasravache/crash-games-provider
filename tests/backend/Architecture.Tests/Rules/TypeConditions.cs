using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent.Conditions;

namespace Apogeu.Architecture.Tests.Rules;

/// <summary>
/// Custom ArchUnitNET conditions that compare dependencies by name. Types outside the loaded assemblies (System.Random,
/// MediatR, double) cannot be passed as <see cref="System.Type"/> to the fluent API without being part of the
/// architecture, so the conditions inspect each dependency target instead. Every failure names the offending targets.
/// </summary>
internal static partial class TypeConditions
{
    private static readonly HashSet<string> FloatingPointTypes = new(StringComparer.Ordinal) { "System.Double", "System.Single" };

    private static readonly (string Type, string Member)[] SystemClockReads =
    [
        ("System.DateTime", "get_Now"),
        ("System.DateTime", "get_UtcNow"),
        ("System.DateTime", "get_Today"),
        ("System.DateTimeOffset", "get_Now"),
        ("System.DateTimeOffset", "get_UtcNow"),
        ("System.TimeProvider", "get_System"),
    ];

    /// <summary>A module layer depends on another module only through that module's Contracts.</summary>
    public static ICondition<IType> DependOnlyOnContractsOfOtherModules() =>
        Condition(
            "only depend on the Contracts of other modules",
            type =>
            {
                var origin = ModuleLayer.Of(type);
                return origin is null
                    ? []
                    : ReferencedTypes(type).Where(target => ModuleLayer.Of(target) is { } layer
                        && !string.Equals(layer.Module, origin.Module, StringComparison.Ordinal)
                        && !string.Equals(layer.Layer, "Contracts", StringComparison.Ordinal));
            },
            "depends on another module's internals");

    /// <summary>The type depends on no type whose namespace is (or is under) one of <paramref name="namespaces"/>.</summary>
    public static ICondition<IType> NotDependOnNamespaces(string what, params string[] namespaces) =>
        Condition(
            "not depend on " + what,
            type => ReferencedTypes(type).Where(target => namespaces.Any(ns => IsInNamespace(target, ns))),
            "depends on " + what);

    /// <summary>The type does not depend on the type named <paramref name="fullName"/>.</summary>
    public static ICondition<IType> NotDependOnType(string fullName) =>
        Condition(
            "not depend on " + fullName,
            type => ReferencedTypes(type).Where(target => string.Equals(target.FullName, fullName, StringComparison.Ordinal)),
            "depends on");

    /// <summary>The type reads no system clock (DateTime.Now/UtcNow/Today, DateTimeOffset.Now/UtcNow, TimeProvider.System).</summary>
    public static ICondition<IType> NotReadSystemClock() =>
        Condition(
            "not read the system clock",
            type => type.Dependencies
                .OfType<MethodCallDependency>()
                .Where(call => SystemClockReads.Any(read =>
                    string.Equals(call.Target.FullName, read.Type, StringComparison.Ordinal)
                    && call.TargetMember.Name.StartsWith(read.Member, StringComparison.Ordinal)))
                .Select(call => call.Target),
            "reads the system clock through");

    /// <summary>No property or field of the type is double or float (nullable included).</summary>
    public static ICondition<IType> HaveNoFloatingPointMembers() =>
        new SimpleCondition<IType>(
            type =>
            {
                var offending = type.Members
                    .Where(member => member is PropertyMember or FieldMember)
                    .Where(member => !member.IsCompilerGenerated && IsFloatingPoint(member))
                    .Select(member => member.FullName)
                    .ToList();
                return new ConditionResult(type, offending.Count == 0, "has double/float members: " + string.Join(", ", offending));
            },
            "have no double or float properties or fields");

    private static bool IsFloatingPoint(IMember member)
    {
        var (memberType, genericArguments) = member switch
        {
            PropertyMember property => (property.Type, property.PropertyTypeDependency.TargetGenericArguments),
            FieldMember field => (field.Type, field.Dependencies.SelectMany(dependency => dependency.TargetGenericArguments)),
            _ => throw new ArgumentOutOfRangeException(nameof(member), member.FullName, "Only properties and fields are checked."),
        };
        return FloatingPointTypes.Contains(memberType.FullName)
            || genericArguments.Any(argument => FloatingPointTypes.Contains(argument.Type.FullName));
    }

    private static SimpleCondition<IType> Condition(string description, Func<IType, IEnumerable<IType>> offendingTargets, string failure) =>
        new SimpleCondition<IType>(
            type =>
            {
                var targets = offendingTargets(type).Select(target => target.FullName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                return new ConditionResult(type, targets.Count == 0, $"{failure} {string.Join(", ", targets)}");
            },
            description);

    /// <summary>Every type the given type depends on, including the generic arguments of each dependency.</summary>
    private static IEnumerable<IType> ReferencedTypes(IType type) =>
        type.Dependencies.SelectMany(dependency =>
            dependency.TargetGenericArguments.Select(argument => argument.Type).Prepend(dependency.Target));

    private static bool IsInNamespace(IType type, string ns) =>
        type.Namespace.FullName.Equals(ns, StringComparison.Ordinal)
        || type.Namespace.FullName.StartsWith(ns + ".", StringComparison.Ordinal);

    /// <summary>The module and layer of a type in the <c>Apogeu.&lt;Module&gt;.&lt;Layer&gt;</c> namespace convention.</summary>
    private sealed partial record ModuleLayer(string Module, string Layer)
    {
        public static ModuleLayer? Of(IType type)
        {
            var match = Pattern().Match(type.Namespace.FullName);
            return match.Success ? new ModuleLayer(match.Groups["module"].Value, match.Groups["layer"].Value) : null;
        }

        [GeneratedRegex(@"^Apogeu\.(?<module>[A-Za-z0-9]+)\.(?<layer>Contracts|Domain|Features|Infrastructure)(\.|$)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
        private static partial Regex Pattern();
    }
}
