using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// Double of the SPEC-0007@2 contract backed by the public vectors (CT-02): round r of the chain is vector r (commitment =
/// its previous seed), and every crash point is the real algorithm v1 of <c>Fairness.Domain</c>. Records every call.
/// </summary>
/// <remarks>
/// <see cref="GetCrashPointAsync"/> is the v2 member of <see cref="IFairnessService"/>: declared public here so that it
/// implements the interface member as soon as SPEC-0007@2 adds it to <c>Fairness.Contracts</c>.
/// </remarks>
internal sealed class VectorFairness(TimeProvider time, IReadOnlyList<FairnessVector> vectors) : IFairnessService
{
    private readonly ChainId _chain = new(Guid.NewGuid());
    private readonly Lock _gate = new();
    private readonly List<FairnessCall> _calls = [];
    private int _issued;

    public IReadOnlyList<FairnessCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public Task<ChainInfo> CreateChainAsync(SalaId sala, int length, CancellationToken ct)
    {
        Record(nameof(CreateChainAsync), 0);
        return Task.FromResult(new ChainInfo(_chain, Seed.FromHex(vectors[0].PreviousSeed), Salt.FromHex(vectors[0].Salt), vectors.Count));
    }

    public Task<RoundSeedCommitment> NextRoundAsync(SalaId sala, CancellationToken ct)
    {
        int index;
        lock (_gate)
        {
            if (_issued == vectors.Count)
            {
                throw new ChainExhaustedException("Every vector was issued.");
            }

            index = ++_issued;
        }

        Record(nameof(NextRoundAsync), index);
        return Task.FromResult(new RoundSeedCommitment(_chain, index, Seed.FromHex(vectors[index - 1].PreviousSeed)));
    }

    public Task<CrashPoint> GetCrashPointAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct)
    {
        Record(nameof(GetCrashPointAsync), roundIndex);
        var vector = Issued(chain, roundIndex);
        return Task.FromResult(CrashPointCalculator.Compute(Seed.FromHex(vector.Seed), Salt.FromHex(vector.Salt), rtp, cap));
    }

    public Task<RevealedRound> RevealAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct)
    {
        Record(nameof(RevealAsync), roundIndex);
        var vector = Issued(chain, roundIndex);
        var seed = Seed.FromHex(vector.Seed);
        var salt = Salt.FromHex(vector.Salt);
        return Task.FromResult(new RevealedRound(seed, Seed.FromHex(vector.PreviousSeed), salt, CrashPointCalculator.Compute(seed, salt, rtp, cap)));
    }

    public CrashPoint ComputeCrashPoint(Seed seed, Salt salt, Rtp rtp, Multiplier cap) => CrashPointCalculator.Compute(seed, salt, rtp, cap);

    public VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed) =>
        FairnessVerifier.Verify(seed, previousSeed, salt, rtp, cap, claimed);

    private FairnessVector Issued(ChainId chain, int roundIndex)
    {
        lock (_gate)
        {
            if (chain != _chain || roundIndex < 1 || roundIndex > _issued)
            {
                throw new RoundNotFoundException($"Round {roundIndex} was not issued.");
            }
        }

        return vectors[roundIndex - 1];
    }

    private void Record(string method, int roundIndex)
    {
        lock (_gate)
        {
            _calls.Add(new FairnessCall(method, roundIndex, time.GetUtcNow()));
        }
    }
}
