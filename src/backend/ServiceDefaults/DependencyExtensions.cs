using Apogeu.ServiceDefaults.Dependencies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Apogeu.ServiceDefaults;

/// <summary>
/// Declares the infrastructure a service depends on: the connection string is required at startup (validated without
/// connecting) and a readiness health check reports the dependency in <c>/health</c>.
/// </summary>
public static class DependencyExtensions
{
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Requires <c>ConnectionStrings:{connectionName}</c> and adds a PostgreSQL check named <paramref name="healthCheckName"/>.</summary>
    public static TBuilder AddPostgresDependency<TBuilder>(this TBuilder builder, string connectionName, string healthCheckName)
        where TBuilder : IHostApplicationBuilder =>
        builder.AddDependency(connectionName, healthCheckName, connectionString => new PostgresHealthCheck(connectionString));

    /// <summary>Requires <c>ConnectionStrings:{connectionName}</c> and adds a Redis check named <paramref name="healthCheckName"/>.</summary>
    public static TBuilder AddRedisDependency<TBuilder>(this TBuilder builder, string connectionName, string healthCheckName)
        where TBuilder : IHostApplicationBuilder =>
        builder.AddDependency(connectionName, healthCheckName, connectionString => new RedisHealthCheck(connectionString));

    /// <summary>Fails host startup with a message naming the key when <c>ConnectionStrings:{connectionName}</c> is missing.</summary>
    public static TBuilder RequireConnectionString<TBuilder>(this TBuilder builder, string connectionName)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        builder.Services
            .AddOptions<RequiredConnectionString>(connectionName)
            .Configure<IConfiguration>((options, configuration) => options.Value = configuration.GetConnectionString(connectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Value),
                $"Missing required configuration 'ConnectionStrings:{connectionName}' (environment variable ConnectionStrings__{connectionName}).")
            .ValidateOnStart();

        return builder;
    }

    private static TBuilder AddDependency<TBuilder, TCheck>(
        this TBuilder builder,
        string connectionName,
        string healthCheckName,
        Func<string, TCheck> createCheck)
        where TBuilder : IHostApplicationBuilder
        where TCheck : class, IHealthCheck
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(healthCheckName);
        builder.RequireConnectionString(connectionName);

        // Keyed singleton: one connection pool per dependency, created on the first check and disposed with the host.
        builder.Services.AddKeyedSingleton(
            healthCheckName,
            (services, _) => createCheck(services.GetRequiredService<IConfiguration>().GetConnectionString(connectionName)!));
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            healthCheckName,
            services => services.GetRequiredKeyedService<TCheck>(healthCheckName),
            failureStatus: HealthStatus.Unhealthy,
            tags: [ServiceDefaultsExtensions.ReadyTag],
            timeout: HealthCheckTimeout));

        return builder;
    }

    private sealed class RequiredConnectionString
    {
        public string? Value { get; set; }
    }
}
