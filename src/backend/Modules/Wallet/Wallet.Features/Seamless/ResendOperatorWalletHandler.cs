using Apogeu.Wallet.Infrastructure.Messaging;
using Apogeu.Wallet.Infrastructure.Seamless;
using Wolverine;

namespace Apogeu.Wallet.Features.Seamless;

/// <summary>
/// Handles <see cref="ResendOperatorWallet"/> (SPEC-0046, ADR-0002/0018): sends the pending credit or rollback again to the
/// operator wallet with the same transaction id and, while the operator gives no definitive answer, schedules the next resend
/// durably with a growing delay (1 s … 5 min) — on any instance, also after a restart. Each pending operation is its own
/// message on a parallel queue (<see cref="WalletModule.ResendQueue"/>), so a slow operator holds no other one.
/// </summary>
public static class ResendOperatorWalletHandler
{
    public static async Task HandleAsync(ResendOperatorWallet command, SeamlessResends resends, IMessageBus bus, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(resends);
        ArgumentNullException.ThrowIfNull(bus);

        if (await resends.ResendAsync(command, ct).ConfigureAwait(false) is { } delay)
        {
            await bus.ScheduleAsync(command with { Attempt = command.Attempt + 1 }, delay).ConfigureAwait(false);
        }
    }
}
