using System.Diagnostics;
using Apogeu.AppHost.Tests.Support;
using Xunit;

namespace Apogeu.AppHost.Tests.Integration;

[Collection(AppHostEnvironmentGroup.Name)]
public sealed class EnvironmentStartupTests
{
    private static readonly TimeSpan HealthyBudget = TimeSpan.FromSeconds(90);

    [Fact(DisplayName = "Dado o AppHost via Aspire.Hosting.Testing, quando iniciado, então todos os recursos chegam a Healthy em até 90 s")]
    [Trait("Spec", "SPEC-0005:IT-01")]
    [Trait("Category", "Performance")]
    public async Task GivenAppHostWhenStartedThenAllResourcesBecomeHealthyWithinNinetySeconds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = await AppHostEnvironment.BuildAsync(cancellationToken);
        AppHostEnvironment.AssertDeclaresContractResources(app);

        var stopwatch = Stopwatch.StartNew();
        await app.StartAsync(cancellationToken).WaitAsync(AppHostEnvironment.StartupTimeout, cancellationToken);
        await Task.WhenAll(AppHostEnvironment.ContractResources
            .Select(resource => AppHostEnvironment.WaitHealthyAsync(app, resource, cancellationToken)));
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed <= HealthyBudget,
            $"All resources became healthy only after {stopwatch.Elapsed.TotalSeconds:F0} s (budget: {HealthyBudget.TotalSeconds:F0} s, images cached).");
    }
}
