using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.LiveBets;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Features.Sessions;
using Apogeu.Betting.Features.Wallet;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apogeu.Betting.Features.PlaceBet;

/// <summary>
/// <c>PlaceBet</c> (SPEC-0022 §6, contract v3; ADR-0007): validates the bet against the operator's limits, the room's cap and
/// the room's current round (the same on every instance), writes it as a pending reservation, debits it
/// (<c>bet:{betId}:debit</c>, sent again while its outcome is unknown) and only then confirms it — no transaction stays open
/// across the wallet call. A round that ended meanwhile (<c>ROUND_CLOSED</c>) or a panel taken meanwhile voids the
/// reservation together with the durable reversal of its debit; a reservation whose debit stays unknown, or left behind
/// (the instance died), is voided by the sweeper after its timeout. A repeated <c>clientBetId</c> answers the original result.
/// </summary>
public sealed class PlaceBetUseCase(
    IBetStore store,
    IRoundFeed feed,
    IOperatorLimits limits,
    ISalaCaps caps,
    IOperatorWallet wallet,
    ICashOutArbiter arbiter,
    BetsNotifier notifier,
    LiveBetsFeed live,
    BettingMetrics metrics,
    IOptions<BettingOptions> options,
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<PlaceBetUseCase> logger)
{
    /// <summary>How long a repeated <c>clientBetId</c> waits for the original call to settle its reservation.</summary>
    private static readonly TimeSpan RepeatWait = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan RepeatPoll = TimeSpan.FromMilliseconds(25);

    public async Task<BettingOutcome<BetAccepted>> PlaceAsync(BettingSession session, PlaceBetRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        var started = time.GetTimestamp();
        using var activity = BettingMetrics.Activities.StartActivity("betting.place_bet");

        if (!session.IsDemo)
        {
            return Refuse(BettingErrorCodes.RealMoneyDisabled);
        }

        if (await store.FindByClientBetIdAsync(session.Owner, request.ClientBetId, ct).ConfigureAwait(false) is { } repeated)
        {
            return await RepeatAsync(session.Owner, repeated, ct).ConfigureAwait(false);
        }

        var betLimits = limits.Get(session.Owner.OperatorId, session.Owner.Currency);
        var rules = new BetRules(betLimits.MinBet.Value, betLimits.MaxBet.Value, caps.CapOf(session.SalaId)?.Value ?? 0m);
        var current = await feed.CurrentAsync(session.SalaId, ct).ConfigureAwait(false);
        var now = time.GetUtcNow();
        var open = await store.ListOpenAsync(session.Owner, ct).ConfigureAwait(false);
        var placement = Bet.Place(
            Guid.CreateVersion7(now),
            session.Owner,
            new BetRequest(request.ClientBetId, request.Panel, request.Amount.Value, request.AutoCashOut?.Value),
            rules,
            current,
            now,
            open);
        if (placement is BetPlacementRejected rejected)
        {
            return await RejectAsync(session.Owner, request.ClientBetId, rejected.Code, ct).ConfigureAwait(false);
        }

        var bet = ((BetPlacementAccepted)placement).Bet;
        activity?.SetTag("betting.bet_id", bet.Id);
        if (await ReserveAsync(bet, session.SalaId, now, ct).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        var outcome = await DebitAndConfirmAsync(bet, session.SalaId, ct).ConfigureAwait(false);
        if (outcome.Value is not null)
        {
            // The room sees the bet (SPEC-0025), with the nickname of the session only.
            live.Accepted(session.SalaId, bet, session.Nickname);

            // Not tied to the caller: the accepted bet catches up with the round even if the connection is gone.
            await CatchUpAsync(bet, session.SalaId, current, CancellationToken.None).ConfigureAwait(false);
            metrics.BetReached(outcome.Value.Status == BetAcceptedStatus.Placed ? "placed" : "queued");
            metrics.Accepted(time.GetElapsedTime(started));
        }

        return outcome;
    }

    /// <summary>A refused bet; the panel may be held by this very clientBetId, accepted by a concurrent call meanwhile.</summary>
    private async Task<BettingOutcome<BetAccepted>> RejectAsync(BetOwner owner, Guid clientBetId, string code, CancellationToken ct) =>
        string.Equals(code, BettingErrorCodes.PanelBusy, StringComparison.Ordinal)
            && await store.FindByClientBetIdAsync(owner, clientBetId, ct).ConfigureAwait(false) is { } concurrent
            ? await RepeatAsync(owner, concurrent, ct).ConfigureAwait(false)
            : Refuse(code);

    /// <summary>Writes the pending reservation of the bet; the answer instead when it collides with another one.</summary>
    private async Task<BettingOutcome<BetAccepted>?> ReserveAsync(Bet bet, string salaId, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            await store.ReserveAsync(bet, salaId, now, ct).ConfigureAwait(false);
        }
        catch (BetConflictException conflict) when (conflict.Conflict == BetConflict.Panel)
        {
            // Another reservation of the player took the panel since the check (it holds it while pending).
            return await RejectAsync(bet.Owner, bet.ClientBetId, BettingErrorCodes.PanelBusy, ct).ConfigureAwait(false);
        }
        catch (BetConflictException conflict)
        {
            // A concurrent PlaceBet with the same clientBetId reserved first: answer what it settles on.
            var original = await store.FindByClientBetIdAsync(bet.Owner, bet.ClientBetId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The bet with clientBetId {bet.ClientBetId} was not found after a conflict.", conflict);
            return await RepeatAsync(bet.Owner, original, ct).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>Debits the reserved bet and confirms it; refuses it (voiding the reservation) when it cannot be accepted.</summary>
    private async Task<BettingOutcome<BetAccepted>> DebitAndConfirmAsync(Bet bet, string salaId, CancellationToken ct)
    {
        var (result, abandoned) = await DebitAsync(bet).ConfigureAwait(false);
        switch (result)
        {
            case WalletInsufficientFunds:
                await store.ReleaseAsync(bet.Id, CancellationToken.None).ConfigureAwait(false);
                return Refuse(BettingErrorCodes.InsufficientFunds);
            case not WalletOk:
                // Outcome unknown even after resending the same debit (SPEC-0021 caller rule): no rollback now — a rollback
                // racing a debit still in flight could be answered TransactionNotFound and miss it. The reservation stays
                // pending and the sweeper reverses it after its timeout; an attempt answered later is watched (IT-12).
                BettingLog.DebitUncertain(logger, bet.Id, result?.GetType().Name ?? "no answer");
                WatchLateDebits(bet, abandoned);
                return Refuse(BettingErrorCodes.WalletUnavailable);
        }

        // A placed bet is confirmed only while its round has not ended (emenda 3: ROUND_CLOSED).
        if (bet.Status == BetStatus.Placed && await RoundEndedAsync(bet, salaId).ConfigureAwait(false))
        {
            return await VoidAsync(bet, BettingErrorCodes.RoundClosed, "round closed").ConfigureAwait(false);
        }

        BetConfirmation confirmation;
        try
        {
            // Not tied to the caller: once debited, the bet is settled even if the connection is gone.
            confirmation = await store.ConfirmAsync(bet.Id, ((WalletOk)result).BalanceAfter.Amount.Value, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // An ambiguous commit: what the database kept decides (the sweeper voids it if still pending).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BettingLog.ConfirmationUnknown(logger, bet.Id, exception);
            return await SettledAsync(bet).ConfigureAwait(false);
        }

        switch (confirmation)
        {
            case BetConfirmation.Confirmed:
                return await SettledAsync(bet).ConfigureAwait(false);
            case BetConfirmation.PanelTaken:
                return await VoidAsync(bet, BettingErrorCodes.PanelBusy, "panel taken").ConfigureAwait(false);
            default:
                // The sweeper voided the reservation before this debit was applied: its reversal may have found no debit.
                await ReverseLateDebitAsync(store, bet).ConfigureAwait(false);
                return Refuse(BettingErrorCodes.WalletUnavailable);
        }
    }

    /// <summary>
    /// A debit the wallet applied after the bet was answered as refused (IT-12): the reservation is voided with the reversal of
    /// the debit when still pending; once swept (its reversal may have found no debit), a new durable reversal of this bet's
    /// debit is stored with the same deterministic rollback id and a reconciliation alert is logged.
    /// </summary>
    private async Task SettleLateDebitAsync(Bet bet)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var lateStore = scope.ServiceProvider.GetRequiredService<IBetStore>();
            if (await lateStore.VoidAsync(bet.Id, CancellationToken.None).ConfigureAwait(false))
            {
                BettingLog.LateDebitVoided(logger, bet.Id);
                return;
            }

            // No longer pending: the reservation was swept (only this call could have confirmed it, and it refused the bet).
            // The clientBetId may already belong to another bet of the player, so nothing is decided by it (M1-bis).
            await ReverseLateDebitAsync(lateStore, bet).ConfigureAwait(false);
        }
    }

    private async Task ReverseLateDebitAsync(IBetStore target, Bet bet)
    {
        BettingLog.ReconciliationLateDebit(logger, bet.Id);
        await target.ReverseLateDebitAsync(bet, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Settles the debit attempts given up on (timed out) if one of them is answered as applied later.</summary>
    private void WatchLateDebits(Bet bet, IReadOnlyList<Task<WalletResult>> abandoned)
    {
        if (abandoned.Count == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var answers = await Task.WhenAll(abandoned.Select(SafeResultAsync)).ConfigureAwait(false);
                if (answers.Any(answer => answer is WalletOk))
                {
                    await SettleLateDebitAsync(bet).ConfigureAwait(false);
                }
            }
#pragma warning disable CA1031 // Background settlement: a failure is logged; the sweeper and the reconciliation stay the safety net.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                BettingLog.LateDebitFailed(logger, bet.Id, exception);
            }
        });
    }

    private static async Task<WalletResult?> SafeResultAsync(Task<WalletResult> attempt)
    {
        try
        {
            return BetTransactions.Original(await attempt.ConfigureAwait(false));
        }
#pragma warning disable CA1031 // An attempt that failed has no answer to settle.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }

    private async Task<bool> RoundEndedAsync(Bet bet, string salaId) =>
        await feed.CurrentAsync(salaId, CancellationToken.None).ConfigureAwait(false) is not { } round
        || round.RoundId != bet.RoundId
        || round.Phase == RoundPhase.Ended;

    /// <summary>
    /// The debit of the bet, unwrapped from a duplicate. Each attempt waits at most <see cref="BettingOptions.DebitAttemptTimeout"/>.
    /// When its outcome is unknown (<see cref="WalletUnavailable"/>, no answer in time or a failure), the same debit (same
    /// transaction id) is sent again (SPEC-0021 caller rule): <see cref="WalletDuplicate"/> means it was applied,
    /// <see cref="WalletOk"/> and <see cref="WalletInsufficientFunds"/> are final. Returns the last answer (or none) and the
    /// attempts given up on, still running.
    /// </summary>
    private async Task<(WalletResult? Result, IReadOnlyList<Task<WalletResult>> Abandoned)> DebitAsync(Bet bet)
    {
        var debit = new WalletOperation(
            BetTransactions.Debit(bet.Id),
            new PlayerRef(bet.Owner.OperatorId, bet.Owner.PlayerId),
            bet.Owner.Currency,
            new Money(bet.Amount),
            bet.RoundId,
            bet.Id);
        var timeout = options.Value.DebitAttemptTimeout;
        var abandoned = new List<Task<WalletResult>>();
        WalletResult? result = null;
        foreach (var delay in BettingOptions.DebitResendDelays)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, time, CancellationToken.None).ConfigureAwait(false);
            }

            // Not tied to the caller nor cancelled at the timeout: the outcome of a debit already sent must be learned, even
            // late (an answer after the timeout is settled by WatchLateDebits); only the wait for it is bounded.
            var attempt = wallet.DebitAsync(debit, CancellationToken.None);
            try
            {
                result = BetTransactions.Original(await attempt.WaitAsync(timeout, time, CancellationToken.None).ConfigureAwait(false));
            }
#pragma warning disable CA1031 // ADR-0007: any failure of the call leaves the debit's outcome unknown, so it is sent again.
            catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
            {
                BettingLog.DebitUncertain(logger, bet.Id, exception.GetType().Name);
                abandoned.Add(attempt);
                result = null;
            }

            if (result is WalletOk or WalletInsufficientFunds or WalletUnavailable { Retryable: false })
            {
                return (result, abandoned);
            }
        }

        return (result, abandoned);
    }

    /// <summary>Voids the reservation with the durable reversal of its debit and refuses the bet with <paramref name="code"/>.</summary>
    private async Task<BettingOutcome<BetAccepted>> VoidAsync(Bet bet, string code, string reason)
    {
        BettingLog.DebitUncertain(logger, bet.Id, reason);

        // If the sweeper voided it first, the reversal is already stored.
        await store.VoidAsync(bet.Id, CancellationToken.None).ConfigureAwait(false);
        return Refuse(code);
    }

    /// <summary>The answer for a bet whose reservation was settled (or is being settled by another call).</summary>
    private async Task<BettingOutcome<BetAccepted>> SettledAsync(Bet bet) =>
        await store.FindByClientBetIdAsync(bet.Owner, bet.ClientBetId, CancellationToken.None).ConfigureAwait(false) is { IsPending: false } accepted
            && accepted.Bet.Id == bet.Id
            ? Accepted(accepted)
            : Refuse(BettingErrorCodes.WalletUnavailable);

    /// <summary>A repeated <c>clientBetId</c>: the original result, once its reservation is settled.</summary>
    private async Task<BettingOutcome<BetAccepted>> RepeatAsync(BetOwner owner, AcceptedBet found, CancellationToken ct)
    {
        var deadline = time.GetUtcNow() + RepeatWait;
        while (found.IsPending && time.GetUtcNow() < deadline)
        {
            await Task.Delay(RepeatPoll, time, ct).ConfigureAwait(false);
            if (await store.FindByClientBetIdAsync(owner, found.Bet.ClientBetId, ct).ConfigureAwait(false) is not { } current)
            {
                // The original call was refused and left nothing: this one is refused alike.
                return Refuse(BettingErrorCodes.WalletUnavailable);
            }

            found = current;
        }

        return found.IsPending ? Refuse(BettingErrorCodes.WalletUnavailable) : Accepted(found);
    }

    /// <summary>
    /// A round event may have been handled while the bet was being debited (its row was not confirmed yet): a placed bet
    /// whose round already flies becomes active, and a queued bet whose next round already opened enters it.
    /// </summary>
    private async Task CatchUpAsync(Bet bet, string salaId, RoundWindow? seen, CancellationToken ct)
    {
        var now = await feed.CurrentAsync(salaId, ct).ConfigureAwait(false);
        if (bet.Status == BetStatus.Placed && now is not null && (now.RoundId != bet.RoundId || now.Phase != RoundPhase.Betting))
        {
            bet.StartFlight(bet.RoundId!.Value);
            if (await store.TransitionAsync(bet, BetStatus.Placed, ct).ConfigureAwait(false))
            {
                // Active after the flight's event was handled: its automatic cash-out is scheduled here (SPEC-0023).
                await RoundEventsHandler.ScheduleAutoAsync(arbiter, salaId, bet, logger, ct).ConfigureAwait(false);
            }
        }
        else if (bet.Status == BetStatus.Queued && now is { Phase: RoundPhase.Betting } opened && opened.RoundId != seen?.RoundId)
        {
            foreach (var activated in await store.ActivateQueuedAsync(salaId, opened, bet.Id, ct).ConfigureAwait(false))
            {
                live.Activated(salaId, activated);
                await notifier.BetActivatedAsync(activated).ConfigureAwait(false);
            }
        }
    }

    private BettingOutcome<BetAccepted> Refuse(string code)
    {
        metrics.RejectedWith(code);
        return new BettingOutcome<BetAccepted>(null, code);
    }

    private static BettingOutcome<BetAccepted> Accepted(AcceptedBet accepted)
    {
        var bet = accepted.Bet;
        return new BettingOutcome<BetAccepted>(
            new BetAccepted(
                bet.Id,
                bet.ClientBetId,
                accepted.AcceptedRoundId?.Value,
                accepted.AcceptedRoundNumber,
                bet.Panel,
                new Money(bet.Amount),
                bet.Owner.Currency.Code,
                bet.AutoCashOut is { } autoCashOut ? new Multiplier(autoCashOut) : null,
                accepted.AcceptedStatus == BetStatus.Placed ? BetAcceptedStatus.Placed : BetAcceptedStatus.Queued,
                new Money(accepted.BalanceAfter)),
            null);
    }
}
