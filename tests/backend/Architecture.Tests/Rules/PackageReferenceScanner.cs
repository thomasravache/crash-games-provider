#pragma warning disable MA0025 // Scaffold (SPEC-0003 review): the Implementer replaces the stub in the Green phase.

namespace Apogeu.Architecture.Tests.Rules;

/// <summary>
/// ADR-0002 at package level: finds NuGet package ids that start with MediatR, AutoMapper or MassTransit
/// (case-insensitive) in Directory.Packages.props and in every packages.lock.json under a repository root.
/// </summary>
public static class PackageReferenceScanner
{
    /// <summary>Forbidden package id prefixes (ADR-0002, commercial licences).</summary>
    public static IReadOnlyList<string> ForbiddenPackageIdPrefixes => throw new NotImplementedException();

    /// <summary>
    /// Every forbidden package found under <paramref name="repositoryRoot"/>, one entry per occurrence, formatted as
    /// <c>&lt;relative file path&gt;: &lt;package id&gt;</c>. Empty when the repository is compliant.
    /// </summary>
    public static IReadOnlyList<string> FindForbiddenPackages(string repositoryRoot) => throw new NotImplementedException();
}
