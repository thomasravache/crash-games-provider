namespace Apogeu.Operators.Features.Configuration;

/// <summary>Configuration section <c>Security:PlayerSession</c>: the key that signs player sessions (secret parameter, ADR-0014).</summary>
public sealed class PlayerSessionOptions
{
    public const string SectionName = "Security:PlayerSession";

    /// <summary>Base64 of at least 32 bytes.</summary>
    public string? SigningKey { get; set; }
}
