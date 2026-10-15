using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Test double of a second operator (SPEC-0025 IT-01, ADR-0005): decorates the Host's real <see cref="IOperatorLimits"/> so
/// the operator <see cref="Operator"/> — which the Development seed does not have — bets with the limits of the demo
/// operator. Its players come with sessions whose claim <c>op</c> is <see cref="Operator"/> (<see cref="BettingSessions.Issue"/>).
/// </summary>
internal sealed class RivalOperatorLimits(IOperatorLimits inner) : IOperatorLimits
{
    /// <summary>The second operator of the room.</summary>
    public const string Operator = "rival-op";

    /// <summary>Registers the decorator over the Host's own <see cref="IOperatorLimits"/>.</summary>
    public static void Register(IServiceCollection services) =>
        services.Decorate<IOperatorLimits>((inner, _) => new RivalOperatorLimits(inner));

    public BetLimits Get(OperatorId op, Currency currency) =>
        inner.Get(string.Equals(op.Value, Operator, StringComparison.Ordinal) ? new OperatorId(BettingSessions.Operator) : op, currency);
}
