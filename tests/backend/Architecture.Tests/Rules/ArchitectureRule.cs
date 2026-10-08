using ArchUnitNET.Fluent;

namespace Apogeu.Architecture.Tests.Rules;

/// <summary>An architecture rule taken from an ADR: a short name, the ADR it enforces and the ArchUnitNET rule.</summary>
public sealed record ArchitectureRule(string Name, string Adr, IArchRule Rule);
