using Apogeu.Rounds.Contracts;
using Apogeu.Rounds.Features.CashOut;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Decorator of the Host's <see cref="IAutoCashOutSink"/> (SPEC-0023 IT-08): the first publication of an executed automatic
/// cash-out fails (the outbox is unreachable for a moment); the following ones reach the real sink.
/// </summary>
internal sealed class FailingAutoCashOutSink(IAutoCashOutSink inner) : IAutoCashOutSink
{
    private int _failures;

    /// <summary>How many publications this decorator failed.</summary>
    public int Failures => Volatile.Read(ref _failures);

    public Task PublishAsync(AutoCashOutExecuted executed, CancellationToken ct) =>
        Interlocked.CompareExchange(ref _failures, 1, 0) == 0
            ? Task.FromException(new IOException("The publication of the automatic cash-out failed (fault injected by IT-08)."))
            : inner.PublishAsync(executed, ct);
}
