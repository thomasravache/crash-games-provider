namespace Apogeu.Betting.Features.Hubs;

/// <summary>Status of the credit of a cash-out (<c>"credited"</c> or <c>"pending"</c> on the wire).</summary>
public enum CreditStatus
{
    /// <summary>The wallet confirmed the credit.</summary>
    Credited,

    /// <summary>The wallet did not confirm it yet; it is retried durably until it does (ADR-0007).</summary>
    Pending,
}
