using Microsoft.Extensions.Options;

namespace Apogeu.Betting.Features;

/// <summary>
/// Validates <see cref="BettingOptions"/> at startup (IT-12): a reservation is swept only after its debit attempts are over,
/// so <see cref="BettingOptions.PendingTimeout"/> must exceed <see cref="BettingOptions.DebitBudget"/>.
/// </summary>
internal sealed class BettingOptionsValidator : IValidateOptions<BettingOptions>
{
    public ValidateOptionsResult Validate(string? name, BettingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.DebitAttemptTimeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("Betting:DebitAttemptTimeout must be positive.");
        }

        if (options.SweepInterval <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("Betting:SweepInterval must be positive.");
        }

        return options.PendingTimeout > options.DebitBudget
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Betting:PendingTimeout ({options.PendingTimeout}) must exceed the debit attempts of a bet ({options.DebitBudget}: " +
                $"{BettingOptions.DebitResendDelays.Count} attempts of Betting:DebitAttemptTimeout {options.DebitAttemptTimeout} plus their delays), " +
                "so no reservation is swept while its debit is still being tried.");
    }
}
