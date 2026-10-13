using System.Text.Json.Serialization;

namespace Apogeu.Betting.Features.MyBets;

/// <summary>Status of a bet in the personal history (SPEC-0025 §6), as on the wire.</summary>
public enum MyBetStatus
{
    /// <summary><c>"placed"</c>.</summary>
    Placed,

    /// <summary><c>"queued"</c>.</summary>
    Queued,

    /// <summary><c>"active"</c>.</summary>
    Active,

    /// <summary><c>"cashed_out"</c>.</summary>
    [JsonStringEnumMemberName("cashed_out")]
    CashedOut,

    /// <summary><c>"lost"</c>.</summary>
    Lost,

    /// <summary><c>"cancelled"</c>.</summary>
    Cancelled,

    /// <summary><c>"refunded"</c>.</summary>
    Refunded,
}
