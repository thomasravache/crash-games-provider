using ArchUnitNET.Fluent;

#pragma warning disable MA0025 // Scaffold (SPEC-0003 phase 1): every stub is replaced by the rule in the Green phase.

namespace Apogeu.Architecture.Tests.Rules;

/// <summary>
/// Architecture rules of ADR-0001, ADR-0002 and ADR-0006 (SPEC-0003). Modules and layers are identified by namespace,
/// following the solution convention <c>Apogeu.&lt;Module&gt;.&lt;Layer&gt;</c> (RootNamespace in Directory.Build.props), so the
/// same rule applies to the real assemblies (IT-01) and to the fixture assemblies with known violations (UT-01..UT-04).
/// </summary>
public static class ArchitectureRules
{
    /// <summary>ADR-0001: no module references the Features, Domain or Infrastructure of another module.</summary>
    public static IArchRule ModuleBoundaries() => throw new NotImplementedException();

    /// <summary>ADR-0002: no type depends on MediatR, AutoMapper or MassTransit (commercial licences).</summary>
    public static IArchRule ForbiddenPackages() => throw new NotImplementedException();

    /// <summary>ADR-0006: no type of the Fairness module uses <see cref="Random"/>; seeds come from RandomNumberGenerator.</summary>
    public static IArchRule FairnessUsesCryptographicRandomness() => throw new NotImplementedException();

    /// <summary>ADR-0002: monetary values of Betting, Wallet and Rounds are <see cref="decimal"/>, never double or float.</summary>
    public static IArchRule MonetaryValuesAreDecimal() => throw new NotImplementedException();

    /// <summary>Every rule of ADR-0001, ADR-0002 and ADR-0006 that applies to the structure, including the four above.</summary>
    public static IReadOnlyList<ArchitectureRule> All() => throw new NotImplementedException();
}
