namespace Apogeu.BuildingBlocks.Security;

/// <summary>Error codes of the security contract v1 (SPEC-0009 §6), carried in the <c>code</c> property of error bodies.</summary>
public static class SecurityErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string UnknownKey = "UNKNOWN_KEY";
    public const string InvalidSignature = "INVALID_SIGNATURE";
    public const string TimestampOutOfWindow = "TIMESTAMP_OUT_OF_WINDOW";
    public const string RealMoneyDisabled = "REAL_MONEY_DISABLED";
    public const string ReplayDetected = "REPLAY_DETECTED";
    public const string RateLimited = "RATE_LIMITED";
    public const string LaunchTokenInvalid = "LAUNCH_TOKEN_INVALID";
    public const string LaunchTokenExpired = "LAUNCH_TOKEN_EXPIRED";
    public const string LaunchTokenUsed = "LAUNCH_TOKEN_USED";
    public const string Forbidden = "FORBIDDEN";
}
