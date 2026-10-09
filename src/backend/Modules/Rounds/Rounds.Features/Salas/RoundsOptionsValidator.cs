using Microsoft.Extensions.Options;

namespace Apogeu.Rounds.Features.Salas;

/// <summary>Validates <c>Rounds:Salas</c> at startup: room ids, RTP, cap, growth constant, phase durations and chain length.</summary>
internal sealed class RoundsOptionsValidator : IValidateOptions<RoundsOptions>
{
    /// <summary>Longest chain the Fairness module creates (SPEC-0007 §4).</summary>
    private const int MaxChainLength = 1_000_000;

    public ValidateOptionsResult Validate(string? name, RoundsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        foreach (var (id, sala) in options.Salas)
        {
            var key = $"Rounds:Salas:{id}";
            if (!SalaIdentity.IsValid(id))
            {
                failures.Add($"{key}: the room id must match ^[a-z0-9-]{{1,32}}$.");
            }

            if (sala.Rtp is < 0.90m or > 0.99m)
            {
                failures.Add($"{key}:Rtp must be between 0.90 and 0.99.");
            }

            if (sala.Cap < 1.00m)
            {
                failures.Add($"{key}:Cap must be at least 1.00.");
            }

            // Published with 2 decimal places (SPEC-0008 §6): more places would diverge from the crash points computed.
            if (decimal.Round(sala.Rtp, 2) != sala.Rtp)
            {
                failures.Add($"{key}:Rtp must have at most 2 decimal places.");
            }

            if (decimal.Round(sala.Cap, 2) != sala.Cap)
            {
                failures.Add($"{key}:Cap must have at most 2 decimal places.");
            }

            if (!double.IsFinite(sala.GrowthK) || sala.GrowthK <= 0)
            {
                failures.Add($"{key}:GrowthK must be a positive number.");
            }

            if (sala.BettingDuration < TimeSpan.Zero || sala.CooldownDuration < TimeSpan.Zero)
            {
                failures.Add($"{key}: the phase durations cannot be negative.");
            }

            if (sala.ChainLength is < 1 or > MaxChainLength)
            {
                failures.Add($"{key}:ChainLength must be between 1 and {MaxChainLength}.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
