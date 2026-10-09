namespace Apogeu.BuildingBlocks.Security;

/// <summary>Outcome of verifying an operator request whose key is known.</summary>
public enum OperatorSignatureVerdict
{
    /// <summary>Signed with the key's secret and inside the time window.</summary>
    Valid,

    /// <summary>The signature does not match the request (<see cref="SecurityErrorCodes.InvalidSignature"/>).</summary>
    InvalidSignature,

    /// <summary>The timestamp is outside the ±300 s window (<see cref="SecurityErrorCodes.TimestampOutOfWindow"/>).</summary>
    TimestampOutOfWindow,
}
