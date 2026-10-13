using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Infrastructure.Observability;
using Apogeu.Wallet.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>
/// Durable resend of credits and rollbacks of the seamless wallet (SPEC-0046 §6, ADR-0002/0018): each one is stored in
/// <c>wallet.pending_resends</c> before its first attempt and stays there until the operator gives a definitive answer; an
/// uncertain attempt schedules the next one with exponential backoff (1 s … 5 min), which the sweep sends — also after a
/// restart of the process. Every attempt is one call of <see cref="SeamlessOperatorWallet"/> with the same transaction id.
/// </summary>
internal sealed class SeamlessResender(
    SeamlessOperatorWallet wallet,
    PendingResendStore store,
    TimeProvider clock,
    ILogger<SeamlessResender> logger,
    WalletMetrics? metrics = null)
{
    /// <summary>Lease of an attempt in flight: past the deadline of one call, so a lost process only delays the resend.</summary>
    internal static readonly TimeSpan Lease = SeamlessOperatorWallet.AttemptDeadline * 6;

    private const string CreditKind = "Credit";
    private const string RollbackKind = "Rollback";

    public Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        var pending = Record(CreditKind, op.TransactionId, op.Player, op.Currency, op.Amount.Value, op.RoundId, op.BetId, original: null);
        return FirstAttemptAsync(pending, ct);
    }

    public Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        var pending = Record(RollbackKind, op.TransactionId, op.Player, op.Currency, amount: null, op.RoundId, op.BetId, op.OriginalTransactionId);
        return FirstAttemptAsync(pending, ct);
    }

    /// <summary>One resend of a row the sweep took (leased).</summary>
    public async Task ResendAsync(PendingResendRecord pending, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pending);
        metrics?.Retried(pending.Kind.ToLowerInvariant());
        await AttemptAsync(pending, pending.Attempts, ct).ConfigureAwait(false);
    }

    private static PendingResendRecord Record(
        string kind, TransactionId transactionId, PlayerRef player, Currency currency, decimal? amount, RoundId? round, Guid betId, TransactionId? original) =>
        new()
        {
            TransactionId = transactionId.Value,
            Kind = kind,
            OperatorId = player.OperatorId.Value,
            PlayerId = player.PlayerId.Value,
            Currency = currency.Code,
            Amount = amount,
            RoundId = round?.Value,
            BetId = betId,
            OriginalTransactionId = original?.Value,
        };

    private async Task<WalletResult> FirstAttemptAsync(PendingResendRecord pending, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        int? attempts;
        try
        {
            attempts = await store.TryTakeAsync(pending, now, now + Lease, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Not stored, so not sent: the caller keeps it (uncertain, retryable) and the operator saw nothing.
            SeamlessLog.PendingNotStored(logger, pending.Kind, pending.TransactionId, error);
            return new WalletUnavailable(Retryable: true);
        }

        // Already pending with an attempt in flight or scheduled: the resend confirms it; nothing is sent twice at once.
        return attempts is { } made
            ? await AttemptAsync(pending, made, ct).ConfigureAwait(false)
            : new WalletUnavailable(Retryable: true);
    }

    /// <summary>One attempt of a taken row: deleted on a definitive answer, rescheduled with backoff otherwise.</summary>
    private async Task<WalletResult> AttemptAsync(PendingResendRecord pending, int attemptsBefore, CancellationToken ct)
    {
        var result = string.Equals(pending.Kind, CreditKind, StringComparison.Ordinal)
            ? await wallet.CreditAsync(CreditOf(pending), ct).ConfigureAwait(false)
            : await wallet.RollbackAsync(RollbackOf(pending), ct).ConfigureAwait(false);

        // The bookkeeping of an attempt made is not abandoned with the caller. If it fails, the lease expires and the row is
        // resent with the same transaction id (a confirmed one then answers DUPLICATE).
        try
        {
            if (result is WalletUnavailable { Retryable: true })
            {
                var attempts = attemptsBefore + 1;
                var next = clock.GetUtcNow() + ResendBackoff.After(attempts);
                await store.RescheduleAsync(pending.TransactionId, attempts, next, CancellationToken.None).ConfigureAwait(false);
                SeamlessLog.ResendScheduled(logger, pending.Kind, pending.TransactionId, attempts + 1, next);
            }
            else
            {
                await store.CompleteAsync(pending.TransactionId, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            SeamlessLog.PendingNotStored(logger, pending.Kind, pending.TransactionId, error);
        }

        return result;
    }

    private static PlayerRef PlayerOf(PendingResendRecord pending) =>
        new(new OperatorId(pending.OperatorId), new PlayerId(pending.PlayerId));

    private static RoundId? RoundOf(PendingResendRecord pending) => pending.RoundId is { } round ? new RoundId(round) : null;

    private static WalletOperation CreditOf(PendingResendRecord pending) =>
        new(
            new TransactionId(pending.TransactionId),
            PlayerOf(pending),
            new Currency(pending.Currency),
            new Money(pending.Amount ?? throw new InvalidOperationException($"Pending credit {pending.TransactionId} has no amount.")),
            RoundOf(pending),
            pending.BetId);

    private static WalletRollback RollbackOf(PendingResendRecord pending) =>
        new(
            new TransactionId(pending.TransactionId),
            new TransactionId(pending.OriginalTransactionId ?? throw new InvalidOperationException($"Pending rollback {pending.TransactionId} has no original transaction.")),
            PlayerOf(pending),
            new Currency(pending.Currency),
            RoundOf(pending),
            pending.BetId);
}
