namespace Apogeu.Operators.Features.Launch;

/// <summary>
/// A launch token issued for a <see cref="LaunchSubject"/> (SPEC-0009 §4, ADR-0015): valid for <see cref="TimeToLive"/>
/// after issue and redeemable once.
/// </summary>
public sealed class LaunchGrant
{
    /// <summary>Time to live of a launch token (ADR-0015: ≤ 60 s).</summary>
    public static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(60);

    private LaunchGrant(string tokenHash, LaunchSubject subject, DateTimeOffset issuedAt, DateTimeOffset expiresAt, bool isUsed)
    {
        TokenHash = tokenHash;
        Subject = subject;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        IsUsed = isUsed;
    }

    public string TokenHash { get; }

    public LaunchSubject Subject { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public bool IsUsed { get; private set; }

    public static LaunchGrant Issue(string tokenHash, LaunchSubject subject, DateTimeOffset issuedAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenHash);
        ArgumentNullException.ThrowIfNull(subject);

        return new LaunchGrant(tokenHash, subject, issuedAt, issuedAt + TimeToLive, isUsed: false);
    }

    /// <summary>Redeems the grant at <paramref name="now"/>: only the first redemption inside the time to live succeeds.</summary>
    public LaunchRedemption Redeem(DateTimeOffset now)
    {
        if (IsUsed)
        {
            return LaunchRedemption.AlreadyUsed;
        }

        if (now > ExpiresAt)
        {
            return LaunchRedemption.Expired;
        }

        IsUsed = true;
        return LaunchRedemption.Redeemed;
    }

    /// <summary>A grant read back from storage, with the expiry it was issued with.</summary>
    internal static LaunchGrant Restore(string tokenHash, LaunchSubject subject, DateTimeOffset issuedAt, DateTimeOffset expiresAt, bool isUsed) =>
        new(tokenHash, subject, issuedAt, expiresAt, isUsed);
}
