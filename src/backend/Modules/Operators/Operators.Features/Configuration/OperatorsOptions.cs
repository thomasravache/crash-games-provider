namespace Apogeu.Operators.Features.Configuration;

/// <summary>Configuration section <c>Operators</c> (SPEC-0009).</summary>
public sealed class OperatorsOptions
{
    public const string SectionName = "Operators";

    /// <summary>Base address of the game client: <c>launchUrl = {GameBaseUrl}/?lt={launchToken}</c>.</summary>
    public Uri? GameBaseUrl { get; set; }

    /// <summary>The <c>demo</c> operator seeded in Development.</summary>
    public DemoOperatorOptions Demo { get; set; } = new();
}
