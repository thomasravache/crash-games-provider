namespace Apogeu.Deploy.Tests.Support;

/// <summary>A release of the fixture repository: a tag with the version text of the fake services and its health.</summary>
/// <param name="Tag">Git tag passed to vps-deploy as the ref.</param>
/// <param name="Version">Text the fake api and demo-casino answer on /version and /casino/version.</param>
/// <param name="Healthy">False: the api has no /health (its container never becomes healthy and the smoke fails).</param>
/// <param name="WithoutService">A service of the contract removed from the compose of this release (contract broken), or null.</param>
internal sealed record Release(string Tag, string Version, bool Healthy = true, string? WithoutService = null);
