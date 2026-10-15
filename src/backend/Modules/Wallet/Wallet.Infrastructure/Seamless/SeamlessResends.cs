using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Infrastructure.Messaging;
using Apogeu.Wallet.Infrastructure.Observability;
using Apogeu.Wallet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>
/// Durable resend of the credits and rollbacks of the seamless wallet (SPEC-0046 §6, ADR-0002/0018): an attempt without a
/// definitive answer of the operator schedules a <see cref="ResendOperatorWallet"/> through the Wolverine outbox of the
/// module's DbContext, and each resend schedules the next one with exponential backoff (1 s … 5 min) until the operator
/// settles it. One chain per transaction id: the guard row <c>wallet.resend_chains</c> is inserted in the same transaction as
/// the scheduled message, so asking for a pending operation again (the durable retries of the caller) makes its own direct
/// attempt but schedules nothing more; the row is deleted when the operator settles it. Every chain is its own message, so a
/// slow or failing operator holds no other one. A resend always
/// goes to the operator wallet (never to the demo one), whatever the mode of the operator now: without a usable
/// configuration, or with its lookup failing, it stays pending and is logged as an error.
/// </summary>
public sealed class SeamlessResends
{
    private const string UniqueViolation = "23505";

    private readonly SeamlessOperatorWallet _wallet;
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<SeamlessResends> _logger;
    private readonly WalletMetrics? _metrics;

    internal SeamlessResends(
        SeamlessOperatorWallet wallet, IServiceScopeFactory scopes, TimeProvider clock, ILogger<SeamlessResends> logger, WalletMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _wallet = wallet;
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// One resend of <paramref name="pending"/>: <see langword="null"/> when the operator settled it, else the wait before the
    /// next one (the caller schedules it with <see cref="ResendOperatorWallet.Attempt"/> plus one).
    /// </summary>
    public async Task<TimeSpan?> ResendAsync(ResendOperatorWallet pending, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var operation = OperationOf(pending.Kind);
        _metrics?.Retried(operation);
        try
        {
            var attempt = pending.Kind == OperatorWalletResendKind.Credit
                ? await _wallet.AttemptCreditAsync(CreditOf(pending), ct).ConfigureAwait(false)
                : await _wallet.AttemptRollbackAsync(RollbackOf(pending), ct).ConfigureAwait(false);
            if (attempt.Settled)
            {
                await EndChainAsync(pending, operation).ConfigureAwait(false);
                return null;
            }
        }
#pragma warning disable CA1031 // SPEC-0046 G4: any failure (the settings lookup, the ledger) keeps the operation pending.
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            SeamlessLog.AttemptFailed(_logger, operation, pending.TransactionId, pending.OperatorId, error.GetType().Name, error);
        }

        var next = pending.Attempt + 1;
        var delay = ResendBackoff.After(next);
        SeamlessLog.ResendScheduled(_logger, operation, pending.TransactionId, pending.OperatorId, next + 1, delay);
        return delay;
    }

    internal Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        var pending = new ResendOperatorWallet(
            OperatorWalletResendKind.Credit, op.TransactionId.Value, op.Player.OperatorId.Value, op.Player.PlayerId.Value, op.Currency.Code,
            op.Amount.Value, op.RoundId?.Value, op.BetId, OriginalTransactionId: null, Attempt: 1);
        return FirstAttemptAsync(pending, () => _wallet.AttemptCreditAsync(op, ct), ct);
    }

    internal Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        var pending = new ResendOperatorWallet(
            OperatorWalletResendKind.Rollback, op.TransactionId.Value, op.Player.OperatorId.Value, op.Player.PlayerId.Value, op.Currency.Code,
            Amount: null, op.RoundId?.Value, op.BetId, op.OriginalTransactionId.Value, Attempt: 1);
        return FirstAttemptAsync(pending, () => _wallet.AttemptRollbackAsync(op, ct), ct);
    }

    private static string OperationOf(OperatorWalletResendKind kind) => kind == OperatorWalletResendKind.Credit ? "credit" : "rollback";

    private static PlayerRef PlayerOf(ResendOperatorWallet pending) =>
        new(new OperatorId(pending.OperatorId), new PlayerId(pending.PlayerId));

    private static RoundId? RoundOf(ResendOperatorWallet pending) => pending.RoundId is { } round ? new RoundId(round) : null;

    private static WalletOperation CreditOf(ResendOperatorWallet pending) =>
        new(
            new TransactionId(pending.TransactionId),
            PlayerOf(pending),
            new Currency(pending.Currency),
            new Money(pending.Amount ?? throw new InvalidOperationException($"The pending credit {pending.TransactionId} has no amount.")),
            RoundOf(pending),
            pending.BetId);

    private static WalletRollback RollbackOf(ResendOperatorWallet pending) =>
        new(
            new TransactionId(pending.TransactionId),
            new TransactionId(pending.OriginalTransactionId ?? throw new InvalidOperationException($"The pending rollback {pending.TransactionId} has no original transaction.")),
            PlayerOf(pending),
            new Currency(pending.Currency),
            RoundOf(pending),
            pending.BetId);

    /// <summary>The first attempt, made by the caller: without a definitive answer it is scheduled to resend.</summary>
    private async Task<WalletResult> FirstAttemptAsync(ResendOperatorWallet pending, Func<Task<SeamlessAttempt>> attempt, CancellationToken ct)
    {
        var operation = OperationOf(pending.Kind);
        SeamlessAttempt made;
        try
        {
            made = await attempt().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller gave up mid-call: the operator may have applied it, so it is resent until settled.
            await TryScheduleAsync(pending, operation).ConfigureAwait(false);
            throw;
        }
#pragma warning disable CA1031 // SPEC-0046 G4: any failure (the settings lookup, the ledger) keeps the operation pending.
        catch (Exception error)
#pragma warning restore CA1031
        {
            SeamlessLog.AttemptFailed(_logger, operation, pending.TransactionId, pending.OperatorId, error.GetType().Name, error);
            await TryScheduleAsync(pending, operation).ConfigureAwait(false);
            return new WalletUnavailable(Retryable: true);
        }

        if (made.Settled)
        {
            return made.Result;
        }

        // Pending (scheduled now, or by an earlier call whose chain goes on): uncertain for the caller, who retries it
        // durably (SPEC-0046 UT-04); never a permanent refusal while the operation is kept.
        await TryScheduleAsync(pending, operation).ConfigureAwait(false);
        return new WalletUnavailable(Retryable: true);
    }

    private static bool IsUniqueViolation(Exception exception) =>
        exception is PostgresException { SqlState: UniqueViolation } || exception.InnerException is PostgresException { SqlState: UniqueViolation };

    /// <summary>
    /// Starts the resend chain of <paramref name="pending"/>: its guard row and the scheduled message, in one transaction of the
    /// module's DbContext through the Wolverine outbox (ADR-0002/0018). A chain already running for the transaction id (the
    /// guard row exists: primary key violation) rolls this one back, so nothing more is scheduled.
    /// </summary>
    private async Task TryScheduleAsync(ResendOperatorWallet pending, string operation)
    {
        var delay = ResendBackoff.After(pending.Attempt);
        try
        {
            var scope = _scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<WalletDbContext>>();
                outbox.DbContext.ResendChains.Add(new ResendChainRecord
                {
                    TransactionId = pending.TransactionId,
                    Kind = pending.Kind.ToString(),
                    OperatorId = pending.OperatorId,
                    CreatedAt = _clock.GetUtcNow(),
                });
                await outbox.PublishAsync(pending, new DeliveryOptions { ScheduleDelay = delay }).ConfigureAwait(false);
                await outbox.SaveChangesAndFlushMessagesAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (IsUniqueViolation(error))
        {
            SeamlessLog.ResendAlreadyPending(_logger, operation, pending.TransactionId, pending.OperatorId);
            return;
        }
#pragma warning disable CA1031 // Not stored: the caller gets the operation as uncertain and keeps it (durable retry).
        catch (Exception error)
#pragma warning restore CA1031
        {
            SeamlessLog.PendingNotStored(_logger, operation, pending.TransactionId, pending.OperatorId, error);
            return;
        }

        SeamlessLog.ResendScheduled(_logger, operation, pending.TransactionId, pending.OperatorId, pending.Attempt + 1, delay);
    }

    /// <summary>The operator settled the operation: its chain ends and a later request may start a new one.</summary>
    private async Task EndChainAsync(ResendOperatorWallet pending, string operation)
    {
        try
        {
            var scope = _scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
                await db.ResendChains.Where(chain => chain.TransactionId == pending.TransactionId)
                    .ExecuteDeleteAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // Settled at the operator; a guard left behind only keeps a new chain from starting for it.
        catch (Exception error)
#pragma warning restore CA1031
        {
            SeamlessLog.ChainNotEnded(_logger, operation, pending.TransactionId, error);
        }
    }
}
