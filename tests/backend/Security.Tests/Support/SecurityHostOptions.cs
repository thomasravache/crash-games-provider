using Apogeu.TestKit.Logging;

namespace Apogeu.Security.Tests.Support;

/// <summary>How a <see cref="SecurityHost"/> is started.</summary>
internal sealed record SecurityHostOptions
{
    public string Environment { get; init; } = "Development";

    public LogCapture? LogCapture { get; init; }

    public bool WithRoundsHubStandIn { get; init; }

    /// <summary>Issuer and audience of the backoffice tokens (IT-06); unset when the test does not use Keycloak.</summary>
    public Uri? BackofficeAuthority { get; init; }

    public string? BackofficeAudience { get; init; }
}
