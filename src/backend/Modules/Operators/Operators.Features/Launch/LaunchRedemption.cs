namespace Apogeu.Operators.Features.Launch;

/// <summary>Outcome of redeeming a launch grant.</summary>
public enum LaunchRedemption
{
    /// <summary>First use inside the time to live: the grant is now used.</summary>
    Redeemed,

    /// <summary>The grant was already redeemed (<c>LAUNCH_TOKEN_USED</c>).</summary>
    AlreadyUsed,

    /// <summary>The time to live has passed (<c>LAUNCH_TOKEN_EXPIRED</c>).</summary>
    Expired,
}
