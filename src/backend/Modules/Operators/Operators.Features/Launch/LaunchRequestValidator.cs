namespace Apogeu.Operators.Features.Launch;

#pragma warning disable MA0025 // Scaffold of SPEC-0009 (no logic yet): implemented in the green phase.

/// <summary>
/// Validates a launch request (SPEC-0009 §6): missing or unsupported fields are <c>VALIDATION_ERROR</c>; any mode other than
/// <c>demo</c> is <c>REAL_MONEY_DISABLED</c> (ADR-0020: no real money before the compliance spec).
/// </summary>
public static class LaunchRequestValidator
{
    public static LaunchRequestValidation Validate(LaunchRequest request) => throw new NotImplementedException();
}
#pragma warning restore MA0025
