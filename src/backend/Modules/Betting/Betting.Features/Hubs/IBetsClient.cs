namespace Apogeu.Betting.Features.Hubs;

/// <summary>Methods the server calls on the connections of one player of <see cref="BetsHub"/> (SPEC-0022 §6).</summary>
public interface IBetsClient
{
    Task BetActivated(BetActivatedMessage message);
}
