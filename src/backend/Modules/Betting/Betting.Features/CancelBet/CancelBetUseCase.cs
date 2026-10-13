using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.LiveBets;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Features.Sessions;
using Apogeu.Betting.Features.Wallet;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;

namespace Apogeu.Betting.Features.CancelBet;

/// <summary>
/// <c>CancelBet</c> (SPEC-0022 §6, contract v3): a placed bet in the betting phase of its round, or a queued bet, of the
/// session's own player is cancelled together with the durable reversal of its debit (one transaction, Wolverine outbox),
/// so its refund is never lost and its flight can no longer make it active. The refund is then tried once at once: the
/// answer says <c>refunded</c> when the wallet confirms it in time, <c>pending</c> otherwise (the durable reversal goes on).
/// A new <c>CancelBet</c> of a cancelled bet answers the same result; <c>WALLET_UNAVAILABLE</c> is never an answer.
/// </summary>
/// <remarks>
/// The <c>live</c> feed of SPEC-0025 is always registered by the module; it is optional so the use case also runs where the
/// room's live bets do not matter (the unit scenarios of the earlier specs build it without it).
/// </remarks>
public sealed class CancelBetUseCase(
    IBetStore store,
    IRoundFeed feed,
    IOperatorWallet wallet,
    BettingMetrics metrics,
    TimeProvider time,
    ILogger<CancelBetUseCase> logger,
    LiveBetsFeed? live = null)
{
    /// <summary>Attempts when the bet changes (a round event) between reading and cancelling it.</summary>
    private const int Attempts = 3;

    /// <summary>How long the answer waits for the wallet to confirm the refund before answering <c>pending</c>.</summary>
    private static readonly TimeSpan RefundWait = TimeSpan.FromSeconds(3);

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
            if (bet.Status == BetStatus.Cancelled)
            {
                // Idempotent: the result the cancellation answered (pending when it was not recorded yet).
                return Answer(bet, found.Refund ?? new BetRefund(bet.Amount, Confirmed: false, Balance: null));
            }

            var previous = bet.Status;
            var round = await feed.CurrentAsync(found.SalaId, ct).ConfigureAwait(false);
            if (bet.Cancel(round, time.GetUtcNow()) is BetCancellationRejected rejected)
            {
                return Refuse(rejected.Code);
            }

            if (await store.TransitionAsync(bet, previous, ct).ConfigureAwait(false))
            {
                metrics.BetReached("cancelled");
                live?.Cancelled(found.SalaId, bet);
                var refund = await RefundAsync(bet, ct).ConfigureAwait(false);
                return Answer(bet, await RecordAsync(bet, refund).ConfigureAwait(false));
            }
        }

        return Refuse(BettingErrorCodes.CancelNotAllowed);
    }

    /// <summary>One attempt of the refund within <see cref="RefundWait"/>; the durable reversal stored with the cancellation goes on otherwise.</summary>
    private async Task<BetRefund> RefundAsync(Bet bet, CancellationToken ct)
    {
        var player = new PlayerRef(bet.Owner.OperatorId, bet.Owner.PlayerId);
        var rollback = new WalletRollback(
            BetTransactions.Rollback(bet.Id),
            BetTransactions.Debit(bet.Id),
            player,
            bet.Owner.Currency,
            bet.RoundId,
            bet.Id);

        using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
        wait.CancelAfter(RefundWait);
        WalletResult? result;
        try
        {
            result = BetTransactions.Original(await wallet.RollbackAsync(rollback, wait.Token).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // ADR-0007: any failure leaves the refund unconfirmed; the durable reversal retries it.
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            BettingLog.RefundPending(logger, bet.Id, exception.GetType().Name);
            result = null;
        }

        switch (result)
        {
            case WalletOk ok:
                return new BetRefund(bet.Amount, Confirmed: true, ok.BalanceAfter.Amount.Value);
            case WalletTransactionNotFound:
                // Reconciliation: the wallet never had this bet's debit, so nothing was given back.
                BettingLog.ReconciliationNoDebit(logger, bet.Id);
                var balance = await wallet.GetBalanceAsync(player, bet.Owner.Currency, ct).ConfigureAwait(false);
                return new BetRefund(0.00m, Confirmed: true, balance.Amount.Value);
            default:
                BettingLog.RefundPending(logger, bet.Id, result?.GetType().Name ?? "no answer");
                return new BetRefund(bet.Amount, Confirmed: false, Balance: null);
        }
    }

    /// <summary>
    /// Records the refund answer, so a new <c>CancelBet</c> repeats it. If it cannot be recorded, the answer is
    /// <c>pending</c> (what a new <c>CancelBet</c> answers without a record); the durable reversal goes on either way.
    /// </summary>
    private async Task<BetRefund> RecordAsync(Bet bet, BetRefund refund)
    {
        try
        {
            if (await store.TransitionAsync(bet, BetStatus.Cancelled, refund, CancellationToken.None).ConfigureAwait(false))
            {
                return refund;
            }
        }
#pragma warning disable CA1031 // The bet is cancelled and its reversal stored: only the answer's record failed.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BettingLog.RefundNotRecorded(logger, bet.Id, exception);
        }

        return new BetRefund(bet.Amount, Confirmed: false, Balance: null);
    }

    private static BettingOutcome<BetCancelled> Answer(Bet bet, BetRefund refund) =>
        new(
            new BetCancelled(
                bet.Id,
                new Money(refund.Refunded),
                refund.Confirmed ? RefundStatus.Refunded : RefundStatus.Pending,
                refund.Balance is { } balance ? new Money(balance) : null),
            null);

    private BettingOutcome<BetCancelled> Refuse(string code)
    {
        metrics.RejectedWith(code);
        return new BettingOutcome<BetCancelled>(null, code);
    }
}
