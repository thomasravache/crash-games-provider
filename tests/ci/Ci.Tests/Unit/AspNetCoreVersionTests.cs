using System.Text.Json;
using System.Xml.Linq;
using Apogeu.Repository.Tests.Support;
using Xunit;

namespace Apogeu.Ci.Tests.Unit;

/// <summary>
/// UT-01 and UT-02 of SPEC-0058: the ASP.NET Core family pinned in <c>Directory.Packages.props</c> and the SDK required by
/// <c>global.json</c> stay out of the range affected by CVE-2026-70159 / GHSA-334f-vvp4-7g6p (SignalR DoS, 10.0.0–10.0.12,
/// fixed in 10.0.13).
/// </summary>
public sealed class AspNetCoreVersionTests
{
    private const string PackagesFile = "Directory.Packages.props";
    private const string GlobalJsonFile = "global.json";
    private const string Advisory = "CVE-2026-70159 (GHSA-334f-vvp4-7g6p)";

    /// <summary>First ASP.NET Core version out of the vulnerable range of the advisory (≤ 10.0.12).</summary>
    private static readonly Version FirstFixedVersion = new(10, 0, 13);

    /// <summary>
    /// Packages that ship with the ASP.NET Core / EF Core release train and therefore carry its version number (the
    /// "same version as ASP.NET Core" rule of <c>Directory.Packages.props</c>): every <c>Microsoft.AspNetCore.*</c>, every
    /// <c>Microsoft.EntityFrameworkCore*</c>, plus the two <c>Microsoft.Extensions.*</c> packages released from the same
    /// repositories (<c>Logging.Abstractions</c> from dotnet/runtime, <c>ApiDescription.Server</c> from dotnet/aspnetcore).
    /// Microsoft packages with their own versioning (OpenTelemetry, Aspire, <c>Microsoft.Extensions.Http.Resilience</c>,
    /// <c>Microsoft.Extensions.TimeProvider.Testing</c>, test SDK, Npgsql provider…) are deliberately left out.
    /// </summary>
    private static readonly string[] Family =
    [
        "Microsoft.AspNetCore.Mvc.Testing",
        "Microsoft.AspNetCore.SignalR.Client",
        "Microsoft.AspNetCore.OpenApi",
        "Microsoft.AspNetCore.SignalR.StackExchangeRedis",
        "Microsoft.AspNetCore.Authentication.JwtBearer",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.ApiDescription.Server",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Design",
        "Microsoft.EntityFrameworkCore.Relational",
    ];

    /// <summary>Prefixes that put a package in <see cref="Family"/> automatically, so a new member cannot be forgotten.</summary>
    private static readonly string[] FamilyPrefixes = ["Microsoft.AspNetCore.", "Microsoft.EntityFrameworkCore"];

    /// <summary>
    /// SDK → ASP.NET Core runtime correspondence for runtime 10.0.13 (released 2026-10-13), per feature band: the first SDK
    /// of each band that bundles the 10.0.13 runtime. Source: the official .NET 10 release metadata,
    /// https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json (release 10.0.13, <c>sdks</c>:
    /// 10.0.402 and 10.0.113; aspnetcore-runtime 10.0.13; cve-list includes CVE-2026-70159), also listed at
    /// https://dotnet.microsoft.com/download/dotnet/10.0. For comparison, 10.0.12 shipped with 10.0.401 and 10.0.112, and
    /// 10.0.100 shipped with runtime 10.0.0. Bands 2xx and 3xx are out of support and received no 10.0.13 SDK.
    /// </summary>
    private static readonly Dictionary<int, Version> MinimumSdkPerBand = new()
    {
        [1] = new Version(10, 0, 113),
        [4] = new Version(10, 0, 402),
    };

    [Fact(DisplayName = "Dado o Directory.Packages.props, quando a família ASP.NET Core é lida, então toda ela está numa mesma versão ≥ 10.0.13")]
    [Trait("Spec", "SPEC-0058:UT-01")]
    public void GivenPackagesPropsWhenAspNetCoreFamilyIsReadThenItIsAlignedAndOutOfTheVulnerableRange()
    {
        var versions = LoadPackageVersions();

        var unlisted = versions.Keys
            .Where(id => FamilyPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal)))
            .Where(id => !Family.Contains(id, StringComparer.Ordinal))
            .ToList();
        Assert.True(
            unlisted.Count == 0,
            $"{PackagesFile} pins ASP.NET Core / EF Core package(s) missing from the SPEC-0058 family list: "
            + $"{string.Join(", ", unlisted)}. Add them to {nameof(AspNetCoreVersionTests)}.{nameof(Family)}.");

        var missing = Family.Where(id => !versions.ContainsKey(id)).ToList();
        Assert.True(
            missing.Count == 0,
            $"{PackagesFile} no longer pins family package(s) {string.Join(", ", missing)}; update {nameof(Family)}.");

        var offending = Family
            .Select(id => (Id: id, Raw: versions[id]))
            .Where(package => !TryParseRelease(package.Raw, out var version) || version < FirstFixedVersion)
            .Select(package => $"{package.Id} {package.Raw}")
            .ToList();
        var distinct = Family.Select(id => versions[id]).Distinct(StringComparer.Ordinal).ToList();

        Assert.True(
            offending.Count == 0 && distinct.Count == 1,
            $"{PackagesFile}: the ASP.NET Core family must be on one single version ≥ {FirstFixedVersion} "
            + $"(out of the range ≤ 10.0.12 affected by {Advisory}). "
            + $"Out of the rule: {(offending.Count == 0 ? "none" : string.Join(", ", offending))}. "
            + $"Versions found in the family: {string.Join(", ", distinct)}.");
    }

    [Fact(DisplayName = "Dado o global.json, quando lido, então o SDK mínimo traz o runtime do ASP.NET Core ≥ 10.0.13")]
    [Trait("Spec", "SPEC-0058:UT-02")]
    public void GivenGlobalJsonWhenReadThenMinimumSdkBringsAFixedAspNetCoreRuntime()
    {
        var path = RepositoryRoot.Combine(GlobalJsonFile);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var raw = document.RootElement.GetProperty("sdk").GetProperty("version").GetString() ?? string.Empty;

        var required = string.Join(" or ", MinimumSdkPerBand.Values.OrderBy(version => version));
        Assert.True(TryParseRelease(raw, out var sdk), $"{GlobalJsonFile} `sdk.version` `{raw}` is not a release SDK version.");

        var band = sdk.Build / 100;
        var ok = sdk.Major == 10 && sdk.Minor == 0
            && MinimumSdkPerBand.TryGetValue(band, out var minimum) && sdk >= minimum;
        Assert.True(
            ok,
            $"{GlobalJsonFile} requires SDK {raw}, which does not bring the ASP.NET Core runtime ≥ {FirstFixedVersion} "
            + $"(fix of {Advisory}). The minimum SDK must be {required} (feature band {band}xx: "
            + $"{(MinimumSdkPerBand.TryGetValue(band, out var sameBand) ? sameBand.ToString() : "no SDK with runtime 10.0.13")}).");
    }

    private static Dictionary<string, string> LoadPackageVersions()
    {
        var document = XDocument.Load(RepositoryRoot.Combine(PackagesFile));
        return document.Descendants("PackageVersion").ToDictionary(
            element => (string?)element.Attribute("Include") ?? string.Empty,
            element => (string?)element.Attribute("Version") ?? string.Empty,
            StringComparer.Ordinal);
    }

    /// <summary>Parses a stable <c>major.minor.patch</c>; a prerelease (<c>-…</c>) is not accepted as a fixed release.</summary>
    private static bool TryParseRelease(string raw, out Version version)
    {
        version = new Version(0, 0);
        if (raw.Contains('-', StringComparison.Ordinal) || !Version.TryParse(raw, out var parsed) || parsed.Build < 0)
        {
            return false;
        }

        version = parsed;
        return true;
    }
}
