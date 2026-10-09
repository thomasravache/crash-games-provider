namespace Apogeu.Rounds.Features.Live;

/// <summary>Methods the server calls on the clients of a room group of <see cref="RoundsHub"/> (SPEC-0008 §6).</summary>
public interface IRoundsClient
{
    Task RoundBetting(RoundBettingMessage message);

    Task RoundFlying(RoundFlyingMessage message);

    Task RoundSync(RoundSyncMessage message);

    Task RoundCrashed(RoundCrashedMessage message);

    Task RoundAnnulled(RoundAnnulledMessage message);
}
