using Apogeu.Rounds.Contracts;

namespace Apogeu.Rounds.Features.CashOut;

/// <summary>Receives each automatic cash-out the leader executes (SPEC-0023 §6); in the Host it publishes the integration event.</summary>
public interface IAutoCashOutSink
{
    /// <summary>The leader executed <paramref name="executed"/> at its target instant.</summary>
    Task PublishAsync(AutoCashOutExecuted executed, CancellationToken ct);
}
