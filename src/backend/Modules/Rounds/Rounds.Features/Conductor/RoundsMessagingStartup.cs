using Apogeu.Rounds.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Apogeu.Rounds.Features.Conductor;

/// <summary>
/// Starts Wolverine's runtime (registered by the Host, ADR-0002) with the Rounds module, whose conductor publishes the round
/// events through its transactional outbox (SPEC-0022): <see cref="TakeOver"/> moves Wolverine's own hosted services under
/// this one. Wolverine needs its PostgreSQL message store when it starts, so — like the leader election, which waits for
/// Redis — the start waits in the background until the database answers (ADR-0018) instead of failing the host; the
/// outbox waits for <see cref="MessagingReadiness"/>. A host composed without the module's hosted services does not start
/// the messaging either.
/// </summary>
internal sealed partial class RoundsMessagingStartup(
    IServiceProvider services,
    RoundsMessagingStartup.Registrations registrations,
    MessagingReadiness readiness,
    IConfiguration configuration,
    ILogger<RoundsMessagingStartup> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private readonly CancellationTokenSource _stopping = new();
    private IHostedService[] _started = [];
    private Task _starting = Task.CompletedTask;

    /// <summary>
    /// Moves the hosted services registered by Wolverine (<c>UseWolverine</c>, called before <c>AddRoundsModule</c>) under
    /// <see cref="RoundsMessagingStartup"/>, keeping their order; nothing happens without Wolverine.
    /// </summary>
    public static void TakeOver(IServiceCollection services)
    {
        var wolverine = services.Where(d => d.ServiceType == typeof(IHostedService) && IsWolverine(d)).ToList();
        if (wolverine.Count == 0)
        {
            return;
        }

        foreach (var descriptor in wolverine)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(new Registrations(wolverine));
        services.AddHostedService<RoundsMessagingStartup>();
    }

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
            var wolverine = registrations.Descriptors.Select(Resolve).ToArray();
            foreach (var service in wolverine)
            {
                await service.StartAsync(stopping).ConfigureAwait(false);
                _started = [.. _started, service];
            }

            readiness.MarkReady();
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // The host stopped before the messaging started.
        }
#pragma warning disable CA1031 // A failed start must not crash the host silently: it is logged and the outbox keeps waiting.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            MessagingFailed(logger, exception);
        }
    }

    private async Task WaitForDatabaseAsync(CancellationToken stopping)
    {
        var connectionString = configuration.GetConnectionString(RoundsModule.ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

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
                MessagingWaitingForDatabase(logger, exception);
                await Task.Delay(RetryInterval, stopping).ConfigureAwait(false);
            }
        }
    }

    private IHostedService Resolve(ServiceDescriptor descriptor) => descriptor switch
    {
        { ImplementationInstance: IHostedService instance } => instance,
        { ImplementationFactory: { } factory } => (IHostedService)factory(services),
        { ImplementationType: { } type } => (IHostedService)ActivatorUtilities.GetServiceOrCreateInstance(services, type),
        _ => throw new InvalidOperationException("Unsupported registration of a Wolverine hosted service."),
    };

    private static bool IsWolverine(ServiceDescriptor descriptor)
    {
        var assembly = descriptor.ImplementationType?.Assembly
            ?? descriptor.ImplementationInstance?.GetType().Assembly
            ?? descriptor.ImplementationFactory?.Method.DeclaringType?.Assembly;
        return assembly?.GetName().Name is { } name && name.StartsWith("Wolverine", StringComparison.Ordinal);
    }

    [LoggerMessage(EventId = 24, Level = LogLevel.Warning, Message = "The messaging waits for its PostgreSQL message store to answer")]
    private static partial void MessagingWaitingForDatabase(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 25, Level = LogLevel.Critical, Message = "The messaging (Wolverine) failed to start; the round events are not published")]
    private static partial void MessagingFailed(ILogger logger, Exception exception);

    /// <summary>The hosted services Wolverine registered, in their order.</summary>
    internal sealed record Registrations(IReadOnlyList<ServiceDescriptor> Descriptors);
}
