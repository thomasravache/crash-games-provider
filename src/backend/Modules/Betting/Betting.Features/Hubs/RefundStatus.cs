namespace Apogeu.Betting.Features.Hubs;

/// <summary>Status of the refund of a cancelled bet (<c>"refunded"</c> or <c>"pending"</c> on the wire).</summary>
public enum RefundStatus
{
    /// <summary>The wallet confirmed the refund.</summary>
    Refunded,

    /// <summary>The wallet did not confirm it yet; it is retried durably until it does.</summary>
    Pending,
}
