namespace Apogeu.Operators.Features.Launch;

#pragma warning disable MA0025 // Scaffold of SPEC-0009 (no logic yet): implemented in the green phase.

/// <summary>
/// A launch token issued for a <see cref="LaunchSubject"/> (SPEC-0009 §4, ADR-0015): valid for <see cref="TimeToLive"/>
/// after issue and redeemable once.
/// </summary>
public sealed class LaunchGrant
{
    /// <summary>Time to live of a launch token (ADR-0015: ≤ 60 s).</summary>
    public static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(60);

    private LaunchGrant()
    {
    }

    public string TokenHash => throw new NotImplementedException();

    public LaunchSubject Subject => throw new NotImplementedException();

    public DateTimeOffset IssuedAt => throw new NotImplementedException();

    public DateTimeOffset ExpiresAt => throw new NotImplementedException();

    public bool IsUsed => throw new NotImplementedException();

    public static LaunchGrant Issue(string tokenHash, LaunchSubject subject, DateTimeOffset issuedAt) => throw new NotImplementedException();

    /// <summary>Redeems the grant at <paramref name="now"/>: only the first redemption inside the time to live succeeds.</summary>
    public LaunchRedemption Redeem(DateTimeOffset now) => throw new NotImplementedException();
}
#pragma warning restore MA0025
