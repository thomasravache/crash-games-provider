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
using Wolverine;

namespace Apogeu.Betting.Features.PlaceBet;

/// <summary>
/// <c>PlaceBet</c> (SPEC-0022 §6, ADR-0007): validates the bet against the operator's limits and the room's cap, debits it
/// (<c>bet:{betId}:debit</c>) while its row is reserved in an open transaction, and only then accepts it. A repeated
/// <c>clientBetId</c> answers the original result; an uncertain debit is reversed by a durable <see cref="ReverseBetDebit"/>.
/// </summary>
public sealed class PlaceBetUseCase(
    IBetStore store,
    IRoundFeed feed,
    IOperatorLimits limits,
    ISalaCaps caps,
    IOperatorWallet wallet,
    IMessageBus bus,
    BetsNotifier notifier,
    BettingMetrics metrics,
    TimeProvider time,
    ILogger<PlaceBetUseCase> logger)
{
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
            return Accepted(repeated);
        }

        var betLimits = limits.Get(session.Owner.OperatorId, session.Owner.Currency);
        var rules = new BetRules(betLimits.MinBet.Value, betLimits.MaxBet.Value, caps.CapOf(session.SalaId)?.Value ?? 0m);
        var current = feed.Current(session.SalaId);
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
            // The panel may be taken by this very clientBetId, accepted by a concurrent call since the first lookup.
            return string.Equals(rejected.Code, BettingErrorCodes.PanelBusy, StringComparison.Ordinal)
                && await store.FindByClientBetIdAsync(session.Owner, request.ClientBetId, ct).ConfigureAwait(false) is { } concurrent
                ? Accepted(concurrent)
                : Refuse(rejected.Code);
        }

        var bet = ((BetPlacementAccepted)placement).Bet;
        activity?.SetTag("betting.bet_id", bet.Id);

        return await DebitAndAcceptAsync(bet, session.SalaId, current, now, started, ct).ConfigureAwait(false);
    }

    /// <summary>Reserves the bet's row, debits it and commits it; refuses it (nothing kept) when the debit is not applied.</summary>
    private async Task<BettingOutcome<BetAccepted>> DebitAndAcceptAsync(
        Bet bet,
        string salaId,
        RoundWindow? current,
        DateTimeOffset now,
        long started,
        CancellationToken ct)
    {
        IBetReservation reservation;
        try
        {
            reservation = await store.ReserveAsync(bet, salaId, now, ct).ConfigureAwait(false);
        }
        catch (BetConflictException conflict)
        {
            // A concurrent PlaceBet with the same clientBetId committed first (its row may hit the panel's index before the
            // clientBetId's): answer its result. Otherwise the panel is taken.
            if (await store.FindByClientBetIdAsync(bet.Owner, bet.ClientBetId, ct).ConfigureAwait(false) is { } original)
            {
                return Accepted(original);
            }

            return conflict.Conflict == BetConflict.Panel
                ? Refuse(BettingErrorCodes.PanelBusy)
                : throw new InvalidOperationException($"The bet with clientBetId {bet.ClientBetId} was not found after a conflict.", conflict);
        }

        AcceptedBet accepted;
        await using (reservation.ConfigureAwait(false))
        {
            var result = await DebitAsync(bet, ct).ConfigureAwait(false);
            switch (result)
            {
                case WalletOk ok:
                    try
                    {
                        accepted = await reservation.CommitAsync(ok.BalanceAfter.Amount.Value, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Debited but not accepted: give it back.
                        await ReverseAsync(bet, "commit failed").ConfigureAwait(false);
                        throw;
                    }

                    break;
                case WalletInsufficientFunds:
                    return Refuse(BettingErrorCodes.InsufficientFunds);
                default:
                    await ReverseAsync(bet, result?.GetType().Name ?? "no answer").ConfigureAwait(false);
                    return Refuse(BettingErrorCodes.WalletUnavailable);
            }
        }

        await CatchUpAsync(bet, salaId, current, ct).ConfigureAwait(false);
        metrics.BetReached(accepted.AcceptedStatus == BetStatus.Placed ? "placed" : "queued");
        metrics.Accepted(time.GetElapsedTime(started));
        return Accepted(accepted);
    }

    /// <summary>The debit of the bet, unwrapped from a duplicate; <see langword="null"/> when the wallet gave no answer.</summary>
    private async Task<WalletResult?> DebitAsync(Bet bet, CancellationToken ct)
    {
        var debit = new WalletOperation(
            BetTransactions.Debit(bet.Id),
            new PlayerRef(bet.Owner.OperatorId, bet.Owner.PlayerId),
            bet.Owner.Currency,
            new Money(bet.Amount),
            bet.RoundId,
            bet.Id);
        try
        {
            return BetTransactions.Original(await wallet.DebitAsync(debit, ct).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // ADR-0007: any failure of the call leaves the debit uncertain, so it is reversed and the bet refused.
        catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
        {
            BettingLog.DebitUncertain(logger, bet.Id, exception.GetType().Name);
            return null;
        }
    }

    private async Task ReverseAsync(Bet bet, string reason)
    {
        BettingLog.DebitUncertain(logger, bet.Id, reason);

        // Not tied to the caller: the reversal must be scheduled even if the connection is gone.
        await bus.SendAsync(new ReverseBetDebit(
            bet.Id,
            bet.Owner.OperatorId.Value,
            bet.Owner.PlayerId.Value,
            bet.Owner.Currency.Code,
            bet.RoundId?.Value)).ConfigureAwait(false);
    }

    /// <summary>
    /// A round event may have been handled while the bet was being debited (its row was not committed yet): a placed bet
    /// whose round already flies becomes active, and a queued bet whose next round already opened enters it.
    /// </summary>
    private async Task CatchUpAsync(Bet bet, string salaId, RoundWindow? seen, CancellationToken ct)
    {
        var now = feed.Current(salaId);
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
