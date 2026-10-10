namespace Apogeu.Operators.Contracts;

/// <summary>Error codes of the catalog and the limits (SPEC-0020 §6), carried in the <c>code</c> property of error bodies.</summary>
public static class OperatorsErrorCodes
{
    /// <summary>The game does not exist or is not enabled for the operator.</summary>
    public const string GameNotAvailable = "GAME_NOT_AVAILABLE";

    /// <summary>The operator has no limits configured in the currency.</summary>
    public const string CurrencyNotEnabled = "CURRENCY_NOT_ENABLED";
}
