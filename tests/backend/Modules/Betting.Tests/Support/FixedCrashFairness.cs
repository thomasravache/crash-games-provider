using Apogeu.Fairness.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Decorator of the real (scoped) <see cref="IFairnessService"/> for SPEC-0023: every round crashes at
/// <paramref name="crashPoint"/> — the crash point read at the start of the flight and the one revealed after it are both
/// that value, so the real conductor flies and crashes the round there (t_c = ln(crash)/k) instead of at a random point.
/// Everything else (chains, commitments, seeds) is the real service.
/// </summary>
internal sealed class FixedCrashFairness(IFairnessService inner, decimal crashPoint) : IFairnessService
{
    /// <summary>Every round of the Host's room crashes at <paramref name="crashPoint"/>.</summary>
    public static void Register(IServiceCollection services, decimal crashPoint) =>
        services.Decorate<IFairnessService>((inner, _) => new FixedCrashFairness(inner, crashPoint));

    public Task<ChainInfo> CreateChainAsync(SalaId sala, int length, CancellationToken ct) => inner.CreateChainAsync(sala, length, ct);

    public Task<RoundSeedCommitment> NextRoundAsync(SalaId sala, CancellationToken ct) => inner.NextRoundAsync(sala, ct);

    public async Task<CrashPoint> GetCrashPointAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct)
    {
        _ = await inner.GetCrashPointAsync(chain, roundIndex, rtp, cap, ct).ConfigureAwait(false);
        return new CrashPoint(crashPoint);
    }

    public async Task<RevealedRound> RevealAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct)
    {
        var revealed = await inner.RevealAsync(chain, roundIndex, rtp, cap, ct).ConfigureAwait(false);
        return revealed with { CrashPoint = new CrashPoint(crashPoint) };
    }

    public CrashPoint ComputeCrashPoint(Seed seed, Salt salt, Rtp rtp, Multiplier cap) => inner.ComputeCrashPoint(seed, salt, rtp, cap);

    public VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed) =>
        inner.Verify(seed, previousSeed, salt, rtp, cap, claimed);
}
