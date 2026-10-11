using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Infrastructure.Persistence;

namespace Apogeu.Rounds.Features.CashOut;

/// <summary>Publishes each automatic cash-out the leader executes as the integration event <see cref="AutoCashOutExecuted"/> (outbox, ADR-0002).</summary>
internal sealed class OutboxAutoCashOutSink(IRoundOutbox outbox) : IAutoCashOutSink
{
    public Task PublishAsync(AutoCashOutExecuted executed, CancellationToken ct) => outbox.PublishAsync(executed, ct);
}
