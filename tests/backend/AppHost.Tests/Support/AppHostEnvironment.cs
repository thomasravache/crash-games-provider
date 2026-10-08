using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>Starts the real AppHost (Aspire.Hosting.Testing) with the resources the contract declares.</summary>
internal static class AppHostEnvironment
{
    /// <summary>Resources of the SPEC-0005 contract (§6).</summary>
    public static readonly IReadOnlyList<string> ContractResources =
        ["postgres", "redis", "keycloak", "api", "game-pipa", "backoffice", "demo-site"];

    /// <summary>Generous bound for container pulls and the first start; the ≤ 90 s budget is asserted separately.</summary>
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(10);

    public static async Task<DistributedApplication> BuildAsync(CancellationToken cancellationToken)
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.AppHost>(cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken)
            .ConfigureAwait(false);

        return await builder.BuildAsync(cancellationToken).WaitAsync(StartupTimeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fails fast (instead of waiting forever) when the AppHost does not declare a contract resource.</summary>
    public static void AssertDeclaresContractResources(DistributedApplication app)
    {
        var model = app.Services.GetService(typeof(DistributedApplicationModel)) as DistributedApplicationModel;
        var declared = model?.Resources.Select(resource => resource.Name).ToHashSet(StringComparer.Ordinal) ?? [];
        var missing = ContractResources.Where(name => !declared.Contains(name)).ToList();
        Xunit.Assert.True(
            missing.Count == 0,
            $"The AppHost does not declare the contract resources: {string.Join(", ", missing)}. Declared: {string.Join(", ", declared)}.");
    }

    public static async Task WaitHealthyAsync(DistributedApplication app, string resource, CancellationToken cancellationToken) =>
        await app.ResourceNotifications
            .WaitForResourceHealthyAsync(resource, cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken)
            .ConfigureAwait(false);
}
