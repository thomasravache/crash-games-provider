using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Apogeu.BuildingBlocks.Contracts;
using Apogeu.Operators.Contracts;
using Apogeu.Rounds.Contracts;
using Apogeu.Wallet.Contracts;
using Apogeu.Wallet.Domain.Ledger;
using Apogeu.Wallet.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Apogeu.Wallet.Infrastructure.Seamless;

/// <summary>
/// Seamless adapter of <see cref="IOperatorWallet"/> (SPEC-0046, ADR-0007): each operation is one signed
/// <c>POST {walletUrl}/apogeu/v1/{balance|debit|credit|rollback}</c> to the operator's own wallet (public contract
/// <c>docs/integration/operator-wallet-api.md</c>), mapped to the <see cref="WalletResult"/> of SPEC-0021@4; a confirmed
/// operation is mirrored in the ledger with the operator's balance.
/// </summary>
/// <remarks>
/// One attempt per call: a debit has a deadline of 2 s and is never resent; the resends of credits and rollbacks until they
/// are confirmed live outside this class (pending table and sweep). Any answer that is not a 200 with a status of the
/// contract (HTTP error, timeout, network) is uncertain: <see cref="WalletUnavailable"/> retryable. PLAYER_NOT_FOUND is
/// definitive and raises an alert. The secret of the key signs the calls and never reaches a log (ADR-0014).
/// </remarks>
public sealed class SeamlessOperatorWallet : IOperatorWallet
{
    /// <summary>Deadline of a debit (SPEC-0046 §2), enforced here and not by <see cref="HttpClient.Timeout"/>.</summary>
    public static readonly TimeSpan DebitDeadline = TimeSpan.FromSeconds(2);

    /// <summary>Deadline of one attempt of a credit, a rollback or a balance query.</summary>
    public static readonly TimeSpan AttemptDeadline = TimeSpan.FromSeconds(5);

    private const string BalanceOperation = "balance";
    private const string DebitOperation = "debit";
    private const string CreditOperation = "credit";
    private const string RollbackOperation = "rollback";
    private const string ApiPath = "apogeu/v1/";
    private const string UncertainStatus = "uncertain";
    private const string NotConfiguredStatus = "not_configured";
    private const decimal MinAmount = 0.01m;
    private const int AmountScale = 2;
    private const int MaxMirrorAttempts = 32;

    private static readonly MediaTypeHeaderValue JsonContentType = new("application/json") { CharSet = "utf-8" };

    private readonly HttpClient _http;
    private readonly IOperatorWalletSettings _settings;
    private readonly IWalletLedger _ledger;
    private readonly TimeProvider _clock;
    private readonly ILogger<SeamlessOperatorWallet> _logger;
    private readonly WalletMetrics? _metrics;

    public SeamlessOperatorWallet(
        HttpClient http,
        IOperatorWalletSettings settings,
        IWalletLedger ledger,
        TimeProvider clock,
        ILogger<SeamlessOperatorWallet> logger,
        WalletMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _http = http;
        _settings = settings;
        _ledger = ledger;
        _clock = clock;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<WalletResult> DebitAsync(WalletOperation op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        RequireCents(op.Amount.Value, nameof(op));

        using var activity = StartActivity(DebitOperation, op.TransactionId);
        var answer = await CallAsync(op.Player, DebitOperation, op.TransactionId, OperationBody(op), DebitDeadline, ct).ConfigureAwait(false);
        var result = answer.Status switch
        {
            "OK" or "DUPLICATE" => await ConfirmedAsync(answer, MirrorOf(op, LedgerEntryKind.Debit, -op.Amount.Value, answer.Balance!), ct).ConfigureAwait(false),
            "INSUFFICIENT_FUNDS" => new WalletInsufficientFunds(answer.Balance!),
            _ => Unconfirmed(answer, op.Player, DebitOperation, op.TransactionId),
        };
        return Traced(activity, DebitOperation, result);
    }

    public async Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);
        RequireCents(op.Amount.Value, nameof(op));

        using var activity = StartActivity(CreditOperation, op.TransactionId);
        var answer = await CallAsync(op.Player, CreditOperation, op.TransactionId, OperationBody(op), AttemptDeadline, ct).ConfigureAwait(false);
        var result = answer.Status switch
        {
            "OK" or "DUPLICATE" => await ConfirmedAsync(answer, MirrorOf(op, LedgerEntryKind.Credit, op.Amount.Value, answer.Balance!), ct).ConfigureAwait(false),
            _ => Unconfirmed(answer, op.Player, CreditOperation, op.TransactionId),
        };
        return Traced(activity, CreditOperation, result);
    }

    public async Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(op);

        using var activity = StartActivity(RollbackOperation, op.TransactionId);
        var answer = await CallAsync(op.Player, RollbackOperation, op.TransactionId, RollbackBody(op), AttemptDeadline, ct).ConfigureAwait(false);
        var result = answer.Status switch
        {
            "OK" or "DUPLICATE" => await ConfirmedRollbackAsync(answer, op, ct).ConfigureAwait(false),
            "TRANSACTION_NOT_FOUND" => new WalletTransactionNotFound(op.OriginalTransactionId),
            _ => Unconfirmed(answer, op.Player, RollbackOperation, op.TransactionId),
        };
        return Traced(activity, RollbackOperation, result);
    }

    public async Task<Balance> GetBalanceAsync(PlayerRef player, Currency currency, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);

        var body = Json(writer =>
        {
            writer.WriteString("playerId", player.PlayerId.Value);
            writer.WriteString("currency", currency.Code);
        });
        var answer = await CallAsync(player, BalanceOperation, transactionId: null, body, AttemptDeadline, ct).ConfigureAwait(false);
        return answer is { Status: "OK", Balance: { } balance }
            ? balance
            : throw new HttpRequestException("The operator wallet gave no balance.", inner: null, HttpStatusCode.ServiceUnavailable);
    }

    private static void RequireCents(decimal amount, string paramName)
    {
        if (amount < MinAmount || decimal.Round(amount, AmountScale) != amount)
        {
            throw new ArgumentOutOfRangeException(paramName, "The amount of a wallet operation must be a whole number of cents, at least 0.01.");
        }
    }

    private static string Amount(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private static byte[] OperationBody(WalletOperation op) => Json(writer =>
    {
        writer.WriteString("transactionId", op.TransactionId.Value);
        writer.WriteString("playerId", op.Player.PlayerId.Value);
        writer.WriteString("currency", op.Currency.Code);
        writer.WriteString("amount", Amount(op.Amount.Value));
        WriteRound(writer, op.RoundId);
        writer.WriteString("betId", op.BetId.ToString("D"));
    });

    private static byte[] RollbackBody(WalletRollback op) => Json(writer =>
    {
        writer.WriteString("transactionId", op.TransactionId.Value);
        writer.WriteString("originalTransactionId", op.OriginalTransactionId.Value);
        writer.WriteString("playerId", op.Player.PlayerId.Value);
        writer.WriteString("currency", op.Currency.Code);
        WriteRound(writer, op.RoundId);
        writer.WriteString("betId", op.BetId.ToString("D"));
    });

    private static void WriteRound(Utf8JsonWriter writer, RoundId? round)
    {
        if (round is { } value)
        {
            writer.WriteString("roundId", value.Value.ToString("D"));
        }
        else
        {
            writer.WriteNull("roundId");
        }
    }

    private static byte[] Json(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static Uri EndpointOf(Uri walletUrl, string operation)
    {
        var baseUrl = walletUrl.AbsoluteUri.EndsWith('/') ? walletUrl : new Uri(walletUrl.AbsoluteUri + "/");
        return new Uri(baseUrl, ApiPath + operation);
    }

    private static Activity? StartActivity(string type, TransactionId transactionId)
    {
        var activity = WalletMetrics.ActivitySource.StartActivity("wallet." + type, ActivityKind.Client);
        activity?.SetTag("wallet.transaction_id", transactionId.Value);
        activity?.SetTag("wallet.adapter", "seamless");
        return activity;
    }

    private static bool IsTransientDatabaseFailure(Exception error) => error switch
    {
        TimeoutException => true,
        PostgresException => false,
        NpgsqlException => true,
        _ => error.InnerException is { } inner && IsTransientDatabaseFailure(inner),
    };

    private static RemoteAnswer? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        Balance? balance = null;
        if (root.TryGetProperty("balance", out var amount) && amount.ValueKind == JsonValueKind.String
            && root.TryGetProperty("currency", out var currency) && currency.ValueKind == JsonValueKind.String
            && decimal.TryParse(amount.GetString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            balance = new Balance(new Money(value), new Currency(currency.GetString()!));
        }

        return new RemoteAnswer(status.GetString()!, balance);
    }

    private WalletResult Traced(Activity? activity, string type, WalletResult result)
    {
        var name = WalletResultNames.Of(result);
        activity?.SetTag("wallet.result", name);
        if (result is WalletUnavailable)
        {
            activity?.SetStatus(ActivityStatusCode.Error, name);
        }

        _metrics?.Operation(type, name);
        return result;
    }

    /// <summary>A status that is not a confirmation: PLAYER_NOT_FOUND (definitive, alert) or anything else (uncertain).</summary>
    private WalletUnavailable Unconfirmed(RemoteAnswer answer, PlayerRef player, string operation, TransactionId transactionId)
    {
        switch (answer.Status)
        {
            case "PLAYER_NOT_FOUND":
                SeamlessLog.PlayerNotFound(_logger, player.OperatorId.Value, operation, transactionId.Value);
                return new WalletUnavailable(Retryable: false);
            case NotConfiguredStatus:
                return new WalletUnavailable(Retryable: false);
            default:
                return new WalletUnavailable(Retryable: true);
        }
    }

    /// <summary>
    /// One signed call to the operator wallet. Returns the answer, or <see cref="UncertainStatus"/> for anything without a
    /// definitive one (non-200, unreadable body, network failure, deadline).
    /// </summary>
    private async Task<RemoteAnswer> CallAsync(
        PlayerRef player, string operation, TransactionId? transactionId, byte[] body, TimeSpan deadline, CancellationToken ct)
    {
        var id = transactionId?.Value ?? string.Empty;
        var settings = await _settings.GetAsync(player.OperatorId, ct).ConfigureAwait(false);
        if (settings is not { Mode: WalletMode.Seamless, WalletUrl: { } walletUrl, Key: { } key })
        {
            SeamlessLog.NotConfigured(_logger, player.OperatorId.Value, operation, id);
            _metrics?.RemoteCall(operation, NotConfiguredStatus);
            return new RemoteAnswer(NotConfiguredStatus, Balance: null);
        }

        var endpoint = EndpointOf(walletUrl, operation);
        using var timeout = new CancellationTokenSource(deadline, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        string reason;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = JsonContentType;
            var timestamp = _clock.GetUtcNow().ToUnixTimeSeconds();
            request.Headers.TryAddWithoutValidation(OperatorWalletSignature.KeyHeader, key.Id);
            request.Headers.TryAddWithoutValidation(OperatorWalletSignature.TimestampHeader, timestamp.ToString(CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation(
                OperatorWalletSignature.SignatureHeader,
                OperatorWalletSignature.Compute(key.Secret, timestamp, HttpMethod.Post.Method, endpoint.AbsolutePath, body));

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, linked.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var json = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
                if (Parse(json) is { } answer && IsDefinitive(answer, operation))
                {
                    _metrics?.RemoteCall(operation, answer.Status.ToLowerInvariant());
                    return answer;
                }

                reason = "answer outside the contract";
            }
            else
            {
                reason = "HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            reason = "deadline of " + deadline.ToString("c", CultureInfo.InvariantCulture);
        }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException)
        {
            reason = error.GetType().Name;
        }

        SeamlessLog.Uncertain(_logger, player.OperatorId.Value, operation, id, reason);
        _metrics?.RemoteCall(operation, UncertainStatus);
        return new RemoteAnswer(UncertainStatus, Balance: null);
    }

    /// <summary>A status of the contract table that carries what its mapping needs (the balance, when it reports one).</summary>
    private static bool IsDefinitive(RemoteAnswer answer, string operation) => answer.Status switch
    {
        "OK" or "DUPLICATE" or "INSUFFICIENT_FUNDS" => answer.Balance is not null,
        "PLAYER_NOT_FOUND" => true,
        "TRANSACTION_NOT_FOUND" => string.Equals(operation, RollbackOperation, StringComparison.Ordinal),
        _ => false,
    };

    private LedgerEntry MirrorOf(WalletOperation op, LedgerEntryKind kind, decimal playerAmount, Balance balanceAfter) =>
        Mirror(op.TransactionId, kind, op.Player, op.Currency, op.RoundId, op.BetId, originalTransactionId: null, playerAmount, balanceAfter);

    private LedgerEntry Mirror(
        TransactionId transactionId,
        LedgerEntryKind kind,
        PlayerRef player,
        Currency currency,
        RoundId? roundId,
        Guid betId,
        TransactionId? originalTransactionId,
        decimal playerAmount,
        Balance balanceAfter)
    {
        var account = LedgerAccount.OfPlayer(player, currency);
        return new LedgerEntry(
            transactionId,
            kind,
            player,
            currency,
            roundId,
            betId,
            originalTransactionId,
            [
                new LedgerPosting(account, new Money(playerAmount)),
                new LedgerPosting(LedgerAccount.OfHouse(player.OperatorId, currency), new Money(-playerAmount)),
            ],
            balanceAfter.Amount,
            _clock.GetUtcNow());
    }

    /// <summary>The operation confirmed by the operator: mirrored in the ledger, then Ok (or Duplicate of the original Ok).</summary>
    private async Task<WalletResult> ConfirmedAsync(RemoteAnswer answer, LedgerEntry mirror, CancellationToken ct)
    {
        if (!await TryMirrorAsync(mirror, ct).ConfigureAwait(false))
        {
            return new WalletUnavailable(Retryable: true);
        }

        var ok = new WalletOk(answer.Balance!);
        return string.Equals(answer.Status, "DUPLICATE", StringComparison.Ordinal) ? new WalletDuplicate(ok) : ok;
    }

    private async Task<WalletResult> ConfirmedRollbackAsync(RemoteAnswer answer, WalletRollback op, CancellationToken ct)
    {
        var ok = new WalletOk(answer.Balance!);
        WalletResult confirmed = string.Equals(answer.Status, "DUPLICATE", StringComparison.Ordinal) ? new WalletDuplicate(ok) : ok;

        LedgerEntry? debit;
        try
        {
            debit = await _ledger.FindAsync(op.OriginalTransactionId, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (IsTransientDatabaseFailure(error))
        {
            SeamlessLog.MirrorFailed(_logger, op.TransactionId.Value, error);
            return new WalletUnavailable(Retryable: true);
        }

        var account = LedgerAccount.OfPlayer(op.Player, op.Currency);
        var debited = debit is { Kind: LedgerEntryKind.Debit }
            ? debit.Postings.FirstOrDefault(posting => posting.Account == account)
            : null;
        if (debited is null)
        {
            // The debit was never confirmed here (uncertain): neither it nor its refund is in the mirror, which stays even.
            SeamlessLog.RollbackWithoutMirroredDebit(_logger, op.TransactionId.Value, op.OriginalTransactionId.Value);
            return confirmed;
        }

        var mirror = Mirror(
            op.TransactionId, LedgerEntryKind.Rollback, op.Player, op.Currency, op.RoundId, op.BetId, op.OriginalTransactionId,
            -debited.Amount.Value, answer.Balance!);
        return await TryMirrorAsync(mirror, ct).ConfigureAwait(false) ? confirmed : new WalletUnavailable(Retryable: true);
    }

    /// <summary>
    /// Appends <paramref name="entry"/> to the mirror ledger once (an entry already recorded under its id is the same one).
    /// False when the ledger's database is transiently unavailable: the operation is then reported uncertain and resent.
    /// </summary>
    private async Task<bool> TryMirrorAsync(LedgerEntry entry, CancellationToken ct)
    {
        var account = LedgerAccount.OfPlayer(entry.Player, entry.Currency);
        try
        {
            for (var attempt = 0; attempt < MaxMirrorAttempts; attempt++)
            {
                var sum = await _ledger.SumAsync(account, ct).ConfigureAwait(false);
                if (await _ledger.TryAppendAsync(entry, sum, ct).ConfigureAwait(false) != LedgerAppendOutcome.StaleBalance)
                {
                    return true;
                }
            }
        }
        catch (Exception error) when (IsTransientDatabaseFailure(error))
        {
            SeamlessLog.MirrorFailed(_logger, entry.TransactionId.Value, error);
        }

        return false;
    }

    /// <summary>A definitive answer of the operator (or <see cref="UncertainStatus"/>/<see cref="NotConfiguredStatus"/>).</summary>
    private sealed record RemoteAnswer(string Status, Balance? Balance);
}
