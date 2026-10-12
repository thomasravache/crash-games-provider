using System.Reflection;
using Apogeu.Betting.Features.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// In-memory <c>IHubContext&lt;BetsHub, IBetsClient&gt;</c> for the unit tests (SPEC-0024 UT-04), built as <see cref="DispatchProxy"/>
/// so it compiles whatever SignalR adds: every server message sent to any client, group or user is recorded with its target.
/// </summary>
public class RecordingHubContext : DispatchProxy
{
    private Recorder _recorder = null!;
    private Kind _kind;

    private enum Kind
    {
        Context,
        Clients,
        Client,
    }

    /// <summary>A hub context and what it sent.</summary>
    public static (IHubContext<BetsHub, IBetsClient> Hub, Recorder Sent) Create()
    {
        var recorder = new Recorder();
        return (Of<IHubContext<BetsHub, IBetsClient>>(Kind.Context, recorder), recorder);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        return _kind switch
        {
            Kind.Context when string.Equals(targetMethod.Name, "get_Clients", StringComparison.Ordinal) => Of<IHubClients<IBetsClient>>(Kind.Clients, _recorder),
            Kind.Clients => Of<IBetsClient>(Kind.Client, _recorder),
            Kind.Client => _recorder.Record(targetMethod.Name, args is [var message] ? message : null),
            _ => throw new NotSupportedException($"The recording hub context does not answer {targetMethod.Name}."),
        };
    }

    private static T Of<T>(Kind kind, Recorder recorder)
        where T : class
    {
        var proxy = Create<T, RecordingHubContext>();
        var self = (RecordingHubContext)(object)proxy;
        self._kind = kind;
        self._recorder = recorder;
        return proxy;
    }

    /// <summary>The server messages sent, in order.</summary>
    public sealed class Recorder
    {
        private readonly Lock _gate = new();
        private readonly List<(string Target, object? Message)> _sent = [];

        public IReadOnlyList<(string Target, object? Message)> Sent
        {
            get
            {
                lock (_gate)
                {
                    return [.. _sent];
                }
            }
        }

        public Task Record(string target, object? message)
        {
            lock (_gate)
            {
                _sent.Add((target, message));
            }

            return Task.CompletedTask;
        }
    }
}
