using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Apogeu.ServiceDefaults.Messaging;

/// <summary>
/// The Host's ownership of the messaging lifecycle (ADR-0028): <see cref="AddMessagingLifecycle"/> moves the hosted services
/// Wolverine registered under this one, which starts them in the background once the PostgreSQL of the message store answers
/// (a Host must start even with its database still unavailable — tests, the build-time OpenAPI document) and then signals
/// <see cref="WhenReadyAsync"/>, which the Host exposes to the modules as their readiness port. Stopping the Host stops them in reverse order.
/// </summary>
public sealed partial class MessagingLifecycle : IHostedService, IDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private readonly IServiceProvider _services;
    private readonly Registrations _registrations;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MessagingLifecycle> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IHostedService[] _started = [];
    private Task _starting = Task.CompletedTask;

    private MessagingLifecycle(IServiceProvider services, Registrations registrations, IConfiguration configuration, ILogger<MessagingLifecycle> logger)
    {
        _services = services;
        _registrations = registrations;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsReady => _ready.Task.IsCompletedSuccessfully;

    /// <summary>
    /// Takes over the hosted services Wolverine registered (call right after <c>UseWolverine</c>, before the modules) and
    /// registers the lifecycle (the Host exposes its readiness to the modules); <paramref name="connectionName"/> names the
    /// connection string of the message store.
    /// </summary>
    public static IServiceCollection AddMessagingLifecycle(IServiceCollection services, string connectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionName);

        var wolverine = services.Where(d => d.ServiceType == typeof(IHostedService) && IsWolverine(d)).ToList();
        foreach (var descriptor in wolverine)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(provider => new MessagingLifecycle(
            provider,
            new Registrations(wolverine, connectionName),
            provider.GetRequiredService<IConfiguration>(),
            provider.GetRequiredService<ILogger<MessagingLifecycle>>()));
        services.AddHostedService<HostedMessaging>();
        return services;
    }

    /// <summary>Completes once the messaging started.</summary>
    public Task WhenReadyAsync(CancellationToken ct) => _ready.Task.WaitAsync(ct);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _starting = Task.Run(() => StartWhenDatabaseAnswersAsync(_stopping.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        await _starting.ConfigureAwait(false);
        foreach (var service in _started.Reverse())
        {
            await service.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose() => _stopping.Dispose();

    private async Task StartWhenDatabaseAnswersAsync(CancellationToken stopping)
    {
        try
        {
            await WaitForDatabaseAsync(stopping).ConfigureAwait(false);
            foreach (var service in _registrations.Descriptors.Select(Resolve))
            {
                await service.StartAsync(stopping).ConfigureAwait(false);
                _started = [.. _started, service];
            }

            _ready.TrySetResult();
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // The Host stopped before the messaging started.
        }
#pragma warning disable CA1031 // A failed start is logged as critical; the modules keep waiting for the readiness.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            MessagingFailed(_logger, exception);
        }
    }

    private async Task WaitForDatabaseAsync(CancellationToken stopping)
    {
        var connectionString = _configuration.GetConnectionString(_registrations.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var attempt = 0;
        while (true)
        {
            try
            {
                var connection = new NpgsqlConnection(connectionString);
                await using (connection.ConfigureAwait(false))
                {
                    await connection.OpenAsync(stopping).ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                // One warning, then silence: a Host without its database (tests, the build) keeps retrying quietly.
                if (attempt++ == 0)
                {
                    WaitingForDatabase(_logger, exception);
                }

                await Task.Delay(RetryInterval, stopping).ConfigureAwait(false);
            }
        }
    }

    private IHostedService Resolve(ServiceDescriptor descriptor) => descriptor switch
    {
        { ImplementationInstance: IHostedService instance } => instance,
        { ImplementationFactory: { } factory } => (IHostedService)factory(_services),
        { ImplementationType: { } type } => (IHostedService)ActivatorUtilities.GetServiceOrCreateInstance(_services, type),
        _ => throw new InvalidOperationException("Unsupported registration of a Wolverine hosted service."),
    };

    private static bool IsWolverine(ServiceDescriptor descriptor)
    {
        var assembly = descriptor.ImplementationType?.Assembly
            ?? descriptor.ImplementationInstance?.GetType().Assembly
            ?? descriptor.ImplementationFactory?.Method.DeclaringType?.Assembly;
        return assembly?.GetName().Name is { } name && name.StartsWith("Wolverine", StringComparison.Ordinal);
    }

    [LoggerMessage(EventId = 9001, Level = LogLevel.Warning, Message = "The messaging waits for its PostgreSQL message store to answer")]
    private static partial void WaitingForDatabase(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Critical, Message = "The messaging (Wolverine) failed to start; no message is delivered")]
    private static partial void MessagingFailed(ILogger logger, Exception exception);

    /// <summary>The hosted services Wolverine registered, in their order, and the connection string name of its store.</summary>
    private sealed record Registrations(IReadOnlyList<ServiceDescriptor> Descriptors, string ConnectionName);

    /// <summary>The hosted-service registration of the lifecycle (a type of this assembly, like the Host's own services).</summary>
    private sealed class HostedMessaging(MessagingLifecycle lifecycle) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => lifecycle.StartAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => lifecycle.StopAsync(cancellationToken);
    }
}
