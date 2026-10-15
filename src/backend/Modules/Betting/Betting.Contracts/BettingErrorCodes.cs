namespace Apogeu.Betting.Contracts;

/// <summary>Error codes of the hub <c>/hubs/bets</c> (SPEC-0022 §6, contract v3): the message of the <c>HubException</c>.</summary>
public static class BettingErrorCodes
{
    /// <summary>Amount ≤ 0 or with more than 2 decimal places.</summary>
    public const string InvalidAmount = "INVALID_AMOUNT";

    /// <summary>Amount below the minimum bet of the operator in the currency.</summary>
    public const string BetBelowMin = "BET_BELOW_MIN";

    /// <summary>Amount above the maximum bet of the operator in the currency.</summary>
    public const string BetAboveMax = "BET_ABOVE_MAX";

    /// <summary>The wallet refused the debit: the balance does not cover the amount.</summary>
    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";

    /// <summary>The panel already has a non-final bet for the same round (or for the next one).</summary>
    public const string PanelBusy = "PANEL_BUSY";

    /// <summary>Automatic cash-out below 1.01x or above the room's cap.</summary>
    public const string AutoCashOutInvalid = "AUTO_CASHOUT_INVALID";

    /// <summary>The wallet could not confirm the debit; an uncertain debit is reversed automatically (ADR-0007).</summary>
    public const string WalletUnavailable = "WALLET_UNAVAILABLE";

    /// <summary>The session is in real-money mode, which is not enabled.</summary>
    public const string RealMoneyDisabled = "REAL_MONEY_DISABLED";

    /// <summary>The panel is neither 1 nor 2.</summary>
    public const string InvalidPanel = "INVALID_PANEL";

    /// <summary>The round of a placed bet ended before the bet was confirmed; its debit is reversed.</summary>
    public const string RoundClosed = "ROUND_CLOSED";

    /// <summary>The flight of the bet's round already started.</summary>
    public const string CancelNotAllowed = "CANCEL_NOT_ALLOWED";

    /// <summary>No bet with that id belongs to the session's player.</summary>
    public const string BetNotFound = "BET_NOT_FOUND";

    /// <summary>The cash-out was received at or after the crash instant (SPEC-0023 §6).</summary>
    public const string CashOutTooLate = "CASHOUT_TOO_LATE";

    /// <summary>The bet is not in flight (placed, queued or cancelled), so it cannot be cashed out (SPEC-0023 §6).</summary>
    public const string BetNotActive = "BET_NOT_ACTIVE";

    /// <summary>The <c>limit</c> of <c>GET /api/v1/me/bets</c> is outside 1..50 (SPEC-0025 §6): the <c>code</c> of the REST error body.</summary>
    public const string InvalidLimit = "INVALID_LIMIT";

    /// <summary>
    /// The <c>before</c> of <c>GET /api/v1/me/bets</c> is not a cursor the API returned (SPEC-0025 §6: the cursor is opaque);
    /// the <c>code</c> of the REST error body, in the same envelope as <see cref="InvalidLimit"/>.
    /// </summary>
    public const string InvalidCursor = "INVALID_CURSOR";
}
