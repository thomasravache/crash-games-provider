using ArchUnitNET.Fluent;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace Apogeu.Architecture.Tests.Support;

/// <summary>Evaluates an ArchUnitNET rule and returns the description of every failed result.</summary>
internal static class RuleViolations
{
    public static IReadOnlyList<string> Of(IArchRule rule, ArchitectureModel architecture) =>
        rule.Evaluate(architecture)
            .Where(result => !result.Passed)
            .Select(result => result.Description)
            .ToList();

    public static string Describe(IReadOnlyList<string> violations) =>
        violations.Count == 0
            ? "(no violations)"
            : string.Join(Environment.NewLine, violations.Select(violation => "  - " + violation));
}
