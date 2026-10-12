namespace Apogeu.Betting.Features.Hubs;

/// <summary>Status of a settled bet (<c>"lost"</c> on the wire).</summary>
public enum BetSettledStatus
{
    /// <summary>Active when its round crashed, without a cash-out.</summary>
    Lost,
}
