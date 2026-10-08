using System.Globalization;
using Apogeu.Architecture.Tests.Rules;
using Apogeu.Architecture.Tests.Support;
using Xunit;

namespace Apogeu.Architecture.Tests.Unit;

public sealed class ForbiddenPackageIdsTests : IDisposable
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "apogeu-arch-packages-" + Guid.NewGuid().ToString("N"));

    [Fact(DisplayName = "Dados manifestos com MediatR, AutoMapper e MassTransit, quando a verificação de pacotes roda, então reprova cada um")]
    [Trait("Spec", "SPEC-0003:UT-02")]
    [Trait("Category", "Architecture")]
    public void GivenManifestsWithForbiddenPackagesWhenPackageScanRunsThenEachIsReported()
    {
        Assert.Equal(
            ["AutoMapper", "MassTransit", "MediatR"],
            PackageReferenceScanner.ForbiddenPackageIdPrefixes.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);

        // Written at run time into a temp folder: a committed lock file naming MediatR would itself trip the scan.
        Write("Directory.Packages.props", """
            <Project>
              <ItemGroup>
                <PackageVersion Include="MediatR" Version="12.4.1" />
                <PackageVersion Include="Npgsql" Version="10.0.3" />
              </ItemGroup>
            </Project>
            """);
        Write(Path.Combine("src", "Some", "packages.lock.json"), """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "AutoMapper.Extensions.Microsoft.DependencyInjection": { "type": "Direct", "requested": "[12.0.1, )", "resolved": "12.0.1" },
                  "masstransit": { "type": "Transitive", "resolved": "8.3.0" },
                  "Npgsql": { "type": "Transitive", "resolved": "10.0.3" }
                }
              }
            }
            """);

        var found = PackageReferenceScanner.FindForbiddenPackages(_scratch);
        var report = string.Join(Environment.NewLine, found);

        Assert.Contains(found, entry => entry.Contains("Directory.Packages.props", StringComparison.Ordinal) && entry.Contains("MediatR", StringComparison.Ordinal));
        Assert.Contains(found, entry => entry.Contains("packages.lock.json", StringComparison.Ordinal) && entry.Contains("AutoMapper.Extensions.Microsoft.DependencyInjection", StringComparison.Ordinal));
        Assert.Contains(found, entry => entry.Contains("packages.lock.json", StringComparison.Ordinal) && entry.Contains("masstransit", StringComparison.OrdinalIgnoreCase));
        Assert.False(
            found.Any(entry => entry.Contains("Npgsql", StringComparison.Ordinal)),
            string.Create(CultureInfo.InvariantCulture, $"Npgsql is allowed and must not be reported. Found:{Environment.NewLine}{report}"));
    }

    [Fact(DisplayName = "Dado o repositório real, quando a verificação de pacotes roda sobre Directory.Packages.props e todos os packages.lock.json, então não há MediatR, AutoMapper nem MassTransit")]
    [Trait("Spec", "SPEC-0003:UT-02")]
    [Trait("Category", "Architecture")]
    public void GivenRealRepositoryWhenPackageScanRunsThenNoForbiddenPackageIsReferenced()
    {
        var found = PackageReferenceScanner.FindForbiddenPackages(RepositoryRoot.Path);

        Assert.True(
            found.Count == 0,
            "Forbidden packages (ADR-0002) referenced in the repository:" + Environment.NewLine + string.Join(Environment.NewLine, found));
    }

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
        {
            Directory.Delete(_scratch, recursive: true);
        }
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_scratch, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
