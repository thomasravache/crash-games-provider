using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Features.Sessions;
using Apogeu.Betting.Features.Wallet;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Apogeu.Betting.Features.CancelBet;

/// <summary>
/// <c>CancelBet</c> (SPEC-0022 §6): a placed bet in the betting phase of its round, or a queued bet, of the session's own
/// player is cancelled first (so its flight can no longer make it active) and then refunded with <c>RollbackAsync</c>
/// (<c>bet:{betId}:rollback</c>). A refund the wallet does not confirm is retried by a durable <see cref="ReverseBetDebit"/>.
/// </summary>
public sealed class CancelBetUseCase(
    IBetStore store,
    IRoundFeed feed,
    IOperatorWallet wallet,
    IMessageBus bus,
    BettingMetrics metrics,
    TimeProvider time,
    ILogger<CancelBetUseCase> logger)
{
    /// <summary>Attempts when the bet changes (a round event) between reading and cancelling it.</summary>
    private const int Attempts = 3;

    public async Task<BettingOutcome<BetCancelled>> CancelAsync(BettingSession session, CancelBetRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        using var activity = BettingMetrics.Activities.StartActivity("betting.cancel_bet")?.SetTag("betting.bet_id", request.BetId);

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (await store.FindAsync(session.Owner, request.BetId, ct).ConfigureAwait(false) is not { } found)
            {
                return Refuse(BettingErrorCodes.BetNotFound);
            }

            var bet = found.Bet;
            var previous = bet.Status;
            if (bet.Cancel(feed.Current(found.SalaId), time.GetUtcNow()) is BetCancellationRejected rejected)
            {
                return Refuse(rejected.Code);
            }

            if (await store.TransitionAsync(bet, previous, ct).ConfigureAwait(false))
            {
                metrics.BetReached("cancelled");
                return await RefundAsync(bet, ct).ConfigureAwait(false);
            }
        }

        return Refuse(BettingErrorCodes.CancelNotAllowed);
    }

    private async Task<BettingOutcome<BetCancelled>> RefundAsync(Bet bet, CancellationToken ct)
    {
        var player = new PlayerRef(bet.Owner.OperatorId, bet.Owner.PlayerId);
        var rollback = new WalletRollback(
            BetTransactions.Rollback(bet.Id),
            BetTransactions.Debit(bet.Id),
            player,
            bet.Owner.Currency,
            bet.RoundId,
            bet.Id);

        WalletResult? result;
        try
        {
            result = BetTransactions.Original(await wallet.RollbackAsync(rollback, ct).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // ADR-0007: any failure leaves the refund unconfirmed, so it is retried durably.
        catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
        {
            result = null;
            BettingLog.RefundPending(logger, bet.Id, exception.GetType().Name);
        }

        switch (result)
        {
            case WalletOk ok:
                return new BettingOutcome<BetCancelled>(new BetCancelled(bet.Id, new Money(bet.Amount), ok.BalanceAfter.Amount), null);
            case WalletTransactionNotFound:
                var balance = await wallet.GetBalanceAsync(player, bet.Owner.Currency, ct).ConfigureAwait(false);
                return new BettingOutcome<BetCancelled>(new BetCancelled(bet.Id, new Money(bet.Amount), balance.Amount), null);
            default:
                BettingLog.RefundPending(logger, bet.Id, result?.GetType().Name ?? "no answer");
                await bus.SendAsync(new ReverseBetDebit(
                    bet.Id,
                    bet.Owner.OperatorId.Value,
                    bet.Owner.PlayerId.Value,
                    bet.Owner.Currency.Code,
                    bet.RoundId?.Value)).ConfigureAwait(false);
                return Refuse(BettingErrorCodes.WalletUnavailable);
        }
    }

    private BettingOutcome<BetCancelled> Refuse(string code)
    {
        metrics.RejectedWith(code);
        return new BettingOutcome<BetCancelled>(null, code);
    }
}
