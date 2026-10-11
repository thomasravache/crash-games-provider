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

    /// <summary>
    /// The type depends on no type of the given packages, recognised by namespace (<c>MediatR</c>, <c>MediatR.*</c>) or by
    /// assembly name (<c>AutoMapper</c>, <c>AutoMapper.*</c>): some packages declare types in a namespace of another
    /// library, like AutoMapper's <c>AddAutoMapper</c> in Microsoft.Extensions.DependencyInjection.
    /// </summary>
    public static ICondition<IType> NotDependOnPackages(string what, params string[] packages) =>
        new SimpleCondition<IType>(
            type =>
            {
                var targets = ReferencedTypes(type)
                    .Where(target => packages.Any(package => IsInNamespace(target, package) || IsFromPackage(target, package)))
                    .Select(target => $"{target.FullName} (assembly {AssemblyName(target)})")
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList();
                return new ConditionResult(type, targets.Count == 0, $"depends on {what} {string.Join(", ", targets)}");
            },
            "not depend on " + what);

    /// <summary>The type does not depend on the type named <paramref name="fullName"/>.</summary>
    public static ICondition<IType> NotDependOnType(string fullName) =>
        Condition(
            "not depend on " + fullName,
            type => ReferencedTypes(type).Where(target => string.Equals(target.FullName, fullName, StringComparison.Ordinal)),
            "depends on");

    /// <summary>
    /// ADR-0028: the type does not handle the hosted services of the host — no <c>RemoveAll&lt;IHostedService&gt;()</c>, no
    /// resolution of <c>IHostedService</c> or <c>IEnumerable&lt;IHostedService&gt;</c>, no <c>typeof(IHostedService)</c>.
    /// Implementing <c>IHostedService</c> (or deriving from <c>BackgroundService</c>) and registering the module's own hosted
    /// service (<c>AddHostedService&lt;TOwn&gt;</c>) stay allowed.
    /// </summary>
    public static ICondition<IType> NotHandleHostedServicesOfOthers() =>
        Condition(
            "not handle the host's hosted services",
            type => type.Dependencies
                .Where(dependency => dependency is not ImplementsInterfaceDependency)
                .Where(dependency => Mentions(dependency.Target, dependency.TargetGenericArguments, HostedService))
                .Select(dependency => dependency.Target),
            "handles the hosted services through");

    private const string HostedService = "Microsoft.Extensions.Hosting.IHostedService";

    private static bool Mentions(IType target, IEnumerable<GenericArgument> arguments, string fullName) =>
        string.Equals(target.FullName, fullName, StringComparison.Ordinal)
        || arguments.Any(argument => Mentions(argument.Type, argument.GenericArguments, fullName));

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

    /// <summary>
    /// No property or field of the type is double or float (nullable included). Types outside
    /// <paramref name="strictNamespaces"/> are only checked on money-named members (Amount, Stake, Payout, Balance,
    /// Price): there a double may be a curve exponent or a multiplier, never money.
    /// </summary>
    public static ICondition<IType> HaveNoFloatingPointMoney(Regex strictNamespaces) =>
        new SimpleCondition<IType>(
            type =>
            {
                var strict = strictNamespaces.IsMatch(type.Namespace.FullName);
                var offending = type.Members
                    .Where(member => member is PropertyMember or FieldMember)
                    .Where(member => !member.IsCompilerGenerated && (strict || MoneyName().IsMatch(member.Name)) && IsFloatingPoint(member))
                    .Select(member => member.FullName)
                    .ToList();
                return new ConditionResult(type, offending.Count == 0, "has double/float money members: " + string.Join(", ", offending));
            },
            "have no double or float money properties or fields");

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
    internal static IEnumerable<IType> ReferencedTypes(IType type) =>
        type.Dependencies.SelectMany(dependency =>
            dependency.TargetGenericArguments.Select(argument => argument.Type).Prepend(dependency.Target));

    private static bool IsFromPackage(IType type, string package)
    {
        var assembly = AssemblyName(type);
        return assembly.Equals(package, StringComparison.OrdinalIgnoreCase)
            || assembly.StartsWith(package + ".", StringComparison.OrdinalIgnoreCase);
    }

    private static string AssemblyName(IType type) => type.Assembly.FullName.Split(',')[0].Trim();

    [GeneratedRegex("Amount|Stake|Payout|Balance|Price", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MoneyName();

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
