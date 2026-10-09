namespace Apogeu.Rounds.Features.Conductor;

/// <summary>Leadership of this instance per room (lock <c>rounds:leader:{sala}</c> in Redis, TTL 5 s renewed every 1 s).</summary>
public interface IRoundsLeadership
{
    /// <summary>Whether this instance currently holds the lock of the room and conducts it.</summary>
    bool IsLeading(string salaId);
}
