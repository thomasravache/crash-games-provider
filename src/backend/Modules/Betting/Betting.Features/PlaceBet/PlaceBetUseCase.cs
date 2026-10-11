using Apogeu.Betting.Contracts;
using Apogeu.Betting.Domain;
using Apogeu.Betting.Features.Hubs;
using Apogeu.Betting.Features.RoundEvents;
using Apogeu.Betting.Features.Sessions;
using Apogeu.Betting.Features.Wallet;
using Apogeu.Betting.Infrastructure.Persistence;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Microsoft.Extensions.Logging;

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
    BetsNotifier notifier,
    BettingMetrics metrics,
    TimeProvider time,
    ILogger<PlaceBetUseCase> logger)
{
    /// <summary>How long a repeated <c>clientBetId</c> waits for the original call to settle its reservation.</summary>
    private static readonly TimeSpan RepeatWait = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan RepeatPoll = TimeSpan.FromMilliseconds(25);

    /// <summary>The first debit and the resends of the same debit while its outcome is unknown (well within the sweeper's timeout).</summary>
    private static readonly TimeSpan[] DebitResends = [TimeSpan.Zero, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)];

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
        try
        {
            await store.ReserveAsync(bet, session.SalaId, now, ct).ConfigureAwait(false);
        }
        catch (BetConflictException conflict)
        {
            // A concurrent PlaceBet with the same clientBetId reserved first: answer what it settles on.
            var original = await store.FindByClientBetIdAsync(bet.Owner, bet.ClientBetId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The bet with clientBetId {bet.ClientBetId} was not found after a conflict.", conflict);
            return await RepeatAsync(bet.Owner, original, ct).ConfigureAwait(false);
        }

        var outcome = await DebitAndConfirmAsync(bet, session.SalaId, ct).ConfigureAwait(false);
        if (outcome.Value is not null)
        {
            await CatchUpAsync(bet, session.SalaId, current, ct).ConfigureAwait(false);
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

    /// <summary>Debits the reserved bet and confirms it; refuses it (voiding the reservation) when it cannot be accepted.</summary>
    private async Task<BettingOutcome<BetAccepted>> DebitAndConfirmAsync(Bet bet, string salaId, CancellationToken ct)
    {
        var result = await DebitAsync(bet).ConfigureAwait(false);
        switch (result)
        {
            case WalletInsufficientFunds:
                await store.ReleaseAsync(bet.Id, CancellationToken.None).ConfigureAwait(false);
                return Refuse(BettingErrorCodes.InsufficientFunds);
            case not WalletOk:
                // Outcome unknown even after resending the same debit (SPEC-0021 caller rule): no rollback now — a rollback
                // racing a debit still in flight could be answered TransactionNotFound and miss it. The reservation stays
                // pending and the sweeper reverses it after its timeout.
                BettingLog.DebitUncertain(logger, bet.Id, result?.GetType().Name ?? "no answer");
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

        return confirmation switch
        {
            BetConfirmation.Confirmed => await SettledAsync(bet).ConfigureAwait(false),
            BetConfirmation.PanelTaken => await VoidAsync(bet, BettingErrorCodes.PanelBusy, "panel taken").ConfigureAwait(false),
            _ => Refuse(BettingErrorCodes.WalletUnavailable),
        };
    }

    private async Task<bool> RoundEndedAsync(Bet bet, string salaId) =>
        await feed.CurrentAsync(salaId, CancellationToken.None).ConfigureAwait(false) is not { } round
        || round.RoundId != bet.RoundId
        || round.Phase == RoundPhase.Ended;

    /// <summary>
    /// The debit of the bet, unwrapped from a duplicate. When its outcome is unknown (<see cref="WalletUnavailable"/> or no
    /// answer), the same debit (same transaction id) is sent again until the wallet answers it for sure (SPEC-0021 caller
    /// rule): <see cref="WalletDuplicate"/> means it was applied, <see cref="WalletOk"/> and
    /// <see cref="WalletInsufficientFunds"/> are final. Returns <see langword="null"/> or the unavailability when it stays unknown.
    /// </summary>
    private async Task<WalletResult?> DebitAsync(Bet bet)
    {
        var debit = new WalletOperation(
            BetTransactions.Debit(bet.Id),
            new PlayerRef(bet.Owner.OperatorId, bet.Owner.PlayerId),
            bet.Owner.Currency,
            new Money(bet.Amount),
            bet.RoundId,
            bet.Id);
        WalletResult? result = null;
        foreach (var delay in DebitResends)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, time, CancellationToken.None).ConfigureAwait(false);
            }

            try
            {
                // Not tied to the caller: the outcome of a debit already sent must be learned even if the connection is gone.
                result = BetTransactions.Original(await wallet.DebitAsync(debit, CancellationToken.None).ConfigureAwait(false));
            }
#pragma warning disable CA1031 // ADR-0007: any failure of the call leaves the debit's outcome unknown, so it is sent again.
            catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
            {
                BettingLog.DebitUncertain(logger, bet.Id, exception.GetType().Name);
                result = null;
            }

            if (result is WalletOk or WalletInsufficientFunds or WalletUnavailable { Retryable: false })
            {
                return result;
            }
        }

        return result;
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
            await store.TransitionAsync(bet, BetStatus.Placed, ct).ConfigureAwait(false);
        }
        else if (bet.Status == BetStatus.Queued && now is { Phase: RoundPhase.Betting } opened && opened.RoundId != seen?.RoundId)
        {
            foreach (var activated in await store.ActivateQueuedAsync(salaId, opened, bet.Id, ct).ConfigureAwait(false))
            {
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
