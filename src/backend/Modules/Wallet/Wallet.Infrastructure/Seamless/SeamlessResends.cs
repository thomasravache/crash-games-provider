using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Infrastructure.Messaging;
using Apogeu.Wallet.Infrastructure.Observability;
using Apogeu.Wallet.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>
/// Durable resend of the credits and rollbacks of the seamless wallet (SPEC-0046 §6, ADR-0002/0018): an attempt without a
/// definitive answer of the operator schedules a <see cref="ResendOperatorWallet"/> through the Wolverine outbox of the
/// module's DbContext, and each resend schedules the next one with exponential backoff (1 s … 5 min) until the operator
/// settles it. Every message is its own pending operation, so a slow or failing operator holds no other one. A resend always
/// goes to the operator wallet (never to the demo one), whatever the mode of the operator now: without a usable
/// configuration, or with its lookup failing, it stays pending and is logged as an error.
/// </summary>
public sealed class SeamlessResends
{
    private readonly SeamlessOperatorWallet _wallet;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SeamlessResends> _logger;
    private readonly WalletMetrics? _metrics;

    internal SeamlessResends(SeamlessOperatorWallet wallet, IServiceScopeFactory scopes, ILogger<SeamlessResends> logger, WalletMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(logger);

        _wallet = wallet;
        _scopes = scopes;
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

        return await TryScheduleAsync(pending, operation).ConfigureAwait(false) ? made.Result : new WalletUnavailable(Retryable: true);
    }

    /// <summary>Stores the scheduled resend through the outbox of the module's DbContext (ADR-0002/0018); false when it could not.</summary>
    private async Task<bool> TryScheduleAsync(ResendOperatorWallet pending, string operation)
    {
        var delay = ResendBackoff.After(pending.Attempt);
        try
        {
            var scope = _scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<WalletDbContext>>();
                await outbox.PublishAsync(pending, new DeliveryOptions { ScheduleDelay = delay }).ConfigureAwait(false);
                await outbox.SaveChangesAndFlushMessagesAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // Not stored: the caller gets the operation as uncertain and keeps it.
        catch (Exception error)
#pragma warning restore CA1031
        {
            SeamlessLog.PendingNotStored(_logger, operation, pending.TransactionId, pending.OperatorId, error);
            return false;
        }

        SeamlessLog.ResendScheduled(_logger, operation, pending.TransactionId, pending.OperatorId, pending.Attempt + 1, delay);
        return true;
    }
}
