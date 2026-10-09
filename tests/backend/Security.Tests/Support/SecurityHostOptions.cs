using Apogeu.TestKit.Logging;

namespace Apogeu.Security.Tests.Support;

/// <summary>How a <see cref="SecurityHost"/> is started.</summary>
internal sealed record SecurityHostOptions
{
    public string Environment { get; init; } = "Development";

    public LogCapture? LogCapture { get; init; }

    /// <summary>
    /// Keeps the hosted services of the Rounds module (the Development migration of the schema <c>rounds</c>), so the real
    /// rounds hub of SPEC-0008 can answer <c>JoinSala</c> (IT-05).
    /// </summary>
    public bool WithRoundsHub { get; init; }

    /// <summary>Issuer and audience of the backoffice tokens (IT-06); unset when the test does not use Keycloak.</summary>
    public Uri? BackofficeAuthority { get; init; }

    public string? BackofficeAudience { get; init; }
}
