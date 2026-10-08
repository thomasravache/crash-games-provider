using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Slices;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Apogeu.Architecture.Tests.Rules;

/// <summary>
/// Architecture rules of ADR-0001, ADR-0002 and ADR-0006 (SPEC-0003). Modules and layers are identified by namespace,
/// following the solution convention <c>Apogeu.&lt;Module&gt;.&lt;Layer&gt;</c> (RootNamespace in Directory.Build.props), so the
/// same rule applies to the real assemblies (IT-01) and to the fixture assemblies with known violations (UT-01..UT-04).
/// Rules do not require positive results: most module projects are still empty (SPEC-0002).
/// </summary>
public static class ArchitectureRules
{
    private const string SolutionTypes = @"^Apogeu(\.|$)";
    private const string DomainLayers = @"^Apogeu\.[A-Za-z0-9]+\.Domain(\.|$)";
    private const string FairnessModule = @"^Apogeu\.Fairness(\.|$)";
    private const string FairnessDomain = @"^Apogeu\.Fairness\.Domain(\.|$)";

    // Money lives in Betting and Wallet, and crosses Rounds only through its contract (the curve inside Rounds uses double).
    private const string MonetaryNamespaces = @"^Apogeu\.(Betting|Wallet)(\.|$)|^Apogeu\.Rounds\.Contracts(\.|$)";

    /// <summary>ADR-0001: no module references the Features, Domain or Infrastructure of another module.</summary>
    public static IArchRule ModuleBoundaries() =>
        Types().That().ResideInNamespaceMatching(SolutionTypes)
            .Should().FollowCustomCondition(TypeConditions.DependOnlyOnContractsOfOtherModules())
            .Because("ADR-0001: a module only references the Contracts of another module")
            .WithoutRequiringPositiveResults();

    /// <summary>ADR-0001: the Domain of any module references neither ASP.NET Core, EF Core nor Wolverine.</summary>
    public static IArchRule DomainIsFrameworkFree() =>
        Types().That().ResideInNamespaceMatching(DomainLayers)
            .Should().FollowCustomCondition(TypeConditions.NotDependOnNamespaces(
                "ASP.NET Core, EF Core or Wolverine",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "Wolverine"))
            .Because("ADR-0001: Domain projects are free of web, persistence and messaging frameworks")
            .WithoutRequiringPositiveResults();

    /// <summary>ADR-0001: Fairness.Domain has no infrastructure (no EF Core, Redis, HTTP, I/O or system clock).</summary>
    public static IArchRule FairnessDomainIsPure() =>
        Types().That().ResideInNamespaceMatching(FairnessDomain)
            .Should().FollowCustomCondition(TypeConditions.NotDependOnNamespaces(
                "infrastructure",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Extensions",
                "Npgsql",
                "StackExchange.Redis",
                "Wolverine",
                "System.IO",
                "System.Net"))
            .AndShould().FollowCustomCondition(TypeConditions.NotReadSystemClock())
            .Because("ADR-0001: Fairness.Domain only computes and verifies; Fairness.Infrastructure persists the seeds")
            .WithoutRequiringPositiveResults();

    /// <summary>ADR-0001: there are no dependency cycles between modules (one slice per <c>Apogeu.&lt;Module&gt;</c>).</summary>
    public static IArchRule NoCyclesBetweenModules() =>
        SliceRuleDefinition.Slices().Matching("Apogeu.(*)..").Should().BeFreeOfCycles();

    /// <summary>ADR-0002: no type depends on MediatR, AutoMapper or MassTransit (commercial licences).</summary>
    public static IArchRule ForbiddenPackages() =>
        Types().That().ResideInNamespaceMatching(SolutionTypes)
            .Should().FollowCustomCondition(TypeConditions.NotDependOnNamespaces(
                "a forbidden package",
                "MediatR",
                "AutoMapper",
                "MassTransit"))
            .Because("ADR-0002: MediatR, AutoMapper and MassTransit have commercial licences")
            .WithoutRequiringPositiveResults();

    /// <summary>ADR-0006: no type of the Fairness module uses <see cref="Random"/>; seeds come from RandomNumberGenerator.</summary>
    public static IArchRule FairnessUsesCryptographicRandomness() =>
        Types().That().ResideInNamespaceMatching(FairnessModule)
            .Should().FollowCustomCondition(TypeConditions.NotDependOnType("System.Random"))
            .Because("ADR-0006: server seeds are generated only by RandomNumberGenerator")
            .WithoutRequiringPositiveResults();

    /// <summary>ADR-0002: monetary values of Betting, Wallet and Rounds are <see cref="decimal"/>, never double or float.</summary>
    public static IArchRule MonetaryValuesAreDecimal() =>
        Types().That().ResideInNamespaceMatching(MonetaryNamespaces)
            .Should().FollowCustomCondition(TypeConditions.HaveNoFloatingPointMembers())
            .Because("ADR-0002: money is decimal in Betting, Wallet and the Rounds contracts")
            .WithoutRequiringPositiveResults();

    /// <summary>Every rule of ADR-0001, ADR-0002 and ADR-0006 that applies to the structure, including the four above.</summary>
    public static IReadOnlyList<ArchitectureRule> All() =>
    [
        new(nameof(ModuleBoundaries), "ADR-0001", ModuleBoundaries()),
        new(nameof(DomainIsFrameworkFree), "ADR-0001", DomainIsFrameworkFree()),
        new(nameof(FairnessDomainIsPure), "ADR-0001", FairnessDomainIsPure()),
        new(nameof(NoCyclesBetweenModules), "ADR-0001", NoCyclesBetweenModules()),
        new(nameof(ForbiddenPackages), "ADR-0002", ForbiddenPackages()),
        new(nameof(MonetaryValuesAreDecimal), "ADR-0002", MonetaryValuesAreDecimal()),
        new(nameof(FairnessUsesCryptographicRandomness), "ADR-0006", FairnessUsesCryptographicRandomness()),
    ];
}
