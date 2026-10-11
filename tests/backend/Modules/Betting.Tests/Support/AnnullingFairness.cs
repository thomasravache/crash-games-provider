using Apogeu.Fairness.Contracts;

namespace Apogeu.Betting.Tests.Support;

/// <summary>
/// Decorator of the real (scoped) <see cref="IFairnessService"/> (IT-05): once <paramref name="mismatch"/> is armed, the next reveal
/// answers a crash point 0,01 above the one the conductor flew, so the real conductor annuls that round
/// (<c>CRASH_POINT_MISMATCH</c>, SPEC-0008) instead of publishing its crash. Everything else is the real service.
/// </summary>
internal sealed class AnnullingFairness(IFairnessService inner, RevealMismatch mismatch) : IFairnessService
{
    public Task<ChainInfo> CreateChainAsync(SalaId sala, int length, CancellationToken ct) => inner.CreateChainAsync(sala, length, ct);

    public Task<RoundSeedCommitment> NextRoundAsync(SalaId sala, CancellationToken ct) => inner.NextRoundAsync(sala, ct);

    public async Task<RevealedRound> RevealAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct)
    {
        var revealed = await inner.RevealAsync(chain, roundIndex, rtp, cap, ct).ConfigureAwait(false);
        return mismatch.Consume()
            ? revealed with { CrashPoint = new CrashPoint(revealed.CrashPoint.Value + 0.01m) }
            : revealed;
    }

    public Task<CrashPoint> GetCrashPointAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct) =>
        inner.GetCrashPointAsync(chain, roundIndex, rtp, cap, ct);

    public CrashPoint ComputeCrashPoint(Seed seed, Salt salt, Rtp rtp, Multiplier cap) => inner.ComputeCrashPoint(seed, salt, rtp, cap);

    public VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed) =>
        inner.Verify(seed, previousSeed, salt, rtp, cap, claimed);
}
