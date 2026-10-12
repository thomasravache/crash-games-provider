using System.Reflection;
using System.Runtime.ExceptionServices;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Faults of the Host's (scoped) <see cref="IBetStore"/> for SPEC-0024 IT-06 and IT-07, as a <see cref="DispatchProxy"/> over
/// the real store: once armed, the commit of the cash-out of one bet (<c>CashOutAsync</c>) waits for a condition, and the
/// activation of the placed bets at the flight (<c>StartFlightAsync</c>) can be lost. Every other member is the real store.
/// </summary>
public class BetStoreFaults : DispatchProxy
{
    private IBetStore _inner = null!;
    private Plan _plan = null!;

    /// <summary>Wraps the Host's <see cref="IBetStore"/> with <paramref name="plan"/>.</summary>
    public static void Register(IServiceCollection services, Plan plan) =>
        services.Decorate<IBetStore>((inner, _) =>
        {
            var proxy = Create<IBetStore, BetStoreFaults>();
            var self = (BetStoreFaults)(object)proxy;
            self._inner = inner;
            self._plan = plan;
            return proxy;
        });

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (string.Equals(targetMethod.Name, nameof(IBetStore.CashOutAsync), StringComparison.Ordinal)
            && args is [Bet bet, ..]
            && _plan.HoldOf(bet.Id) is { } hold)
        {
            return HeldAsync(hold, targetMethod, args);
        }

        if (string.Equals(targetMethod.Name, nameof(IBetStore.StartFlightAsync), StringComparison.Ordinal) && _plan.ActivationLost)
        {
            _plan.ActivationsLost();
            return Task.FromResult(0);
        }

        return Call(targetMethod, args);
    }

    private object? Call(MethodInfo method, object?[]? args)
    {
        try
        {
            return method.Invoke(_inner, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private async Task<bool> HeldAsync(Func<Task> hold, MethodInfo method, object?[] args)
    {
        await hold().ConfigureAwait(false);
        return await ((Task<bool>)Call(method, args)!).ConfigureAwait(false);
    }

    /// <summary>The faults armed by the test, shared by every scope's store.</summary>
    public sealed class Plan
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<Guid, Func<Task>> _holds = [];
        private int _lostActivations;

        /// <summary>Whether <c>StartFlightAsync</c> does nothing (the placed bets are never activated).</summary>
        public bool ActivationLost { get; set; }

        /// <summary>How many flight activations were lost.</summary>
        public int LostActivations => Volatile.Read(ref _lostActivations);

        /// <summary>The commit of the cash-out of <paramref name="betId"/> waits for <paramref name="hold"/>.</summary>
        public void HoldCashOut(Guid betId, Func<Task> hold)
        {
            lock (_gate)
            {
                _holds[betId] = hold;
            }
        }

        public Func<Task>? HoldOf(Guid betId)
        {
            lock (_gate)
            {
                return _holds.GetValueOrDefault(betId);
            }
        }

        public void ActivationsLost() => Interlocked.Increment(ref _lostActivations);
    }
}
