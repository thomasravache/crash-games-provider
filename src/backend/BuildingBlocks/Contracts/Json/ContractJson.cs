#pragma warning disable MA0025 // Scaffold (SPEC-0006): the Implementer replaces the stub in the Green phase.

using System.Text.Json;

namespace Apogeu.BuildingBlocks.Contracts.Json;

/// <summary>
/// JSON conventions of the contracts (SPEC-0006 §6): web defaults (camelCase), <see cref="Money"/> and
/// <see cref="Multiplier"/> as decimal strings, <see cref="DateTimeOffset"/> as ISO-8601 UTC and enums as camelCase strings.
/// </summary>
public static class ContractJson
{
    /// <summary>Ready-to-use options with every contract convention applied.</summary>
    public static JsonSerializerOptions Options => throw new NotImplementedException();

    /// <summary>Applies the contract conventions to existing options (for example, ASP.NET Core's HTTP JSON options).</summary>
    public static void Configure(JsonSerializerOptions options) => throw new NotImplementedException();
}
