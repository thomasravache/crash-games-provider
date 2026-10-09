using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;

namespace Apogeu.Rounds.Tests.Support;

/// <summary>
/// Double of the SPEC-0007@2 contract for the unit tests: a real hash chain (s_r = h_{N-r}, commitment = SHA256(s_r)) whose
/// crash point of round r is scripted by the test. Records every call with the fake server time.
/// </summary>
/// <remarks>
/// <see cref="GetCrashPointAsync"/> is the v2 member of <see cref="IFairnessService"/>: declared public here so that it
/// implements the interface member as soon as SPEC-0007@2 adds it to <c>Fairness.Contracts</c>.
/// </remarks>
internal sealed class ScriptedFairness : IFairnessService
{
    private readonly TimeProvider _time;
    private readonly decimal[] _crashPoints;
    private readonly byte[][] _links;
    private readonly Salt _salt = new(SHA256.HashData("rounds-tests-salt"u8));
    private readonly ChainId _chain = new(Guid.NewGuid());
    private readonly Lock _gate = new();
    private readonly List<FairnessCall> _calls = [];
    private int _issued;

    public ScriptedFairness(TimeProvider time, params decimal[] crashPoints)
    {
        _time = time;
        _crashPoints = crashPoints;
        _links = new byte[crashPoints.Length + 1][];
        _links[0] = SHA256.HashData("rounds-tests-origin"u8);
        for (var i = 1; i < _links.Length; i++)
        {
            _links[i] = SHA256.HashData(_links[i - 1]);
        }
    }

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

    private int Length => _crashPoints.Length;

    /// <summary>s_r = h_{N-r}.</summary>
    public Seed SeedOf(int roundIndex) => new(_links[Length - roundIndex]);

    public Task<ChainInfo> CreateChainAsync(SalaId sala, int length, CancellationToken ct)
    {
        Record(nameof(CreateChainAsync), 0);
        return Task.FromResult(new ChainInfo(_chain, new Seed(_links[Length]), _salt, Length));
    }

    public Task<RoundSeedCommitment> NextRoundAsync(SalaId sala, CancellationToken ct)
    {
        int index;
        lock (_gate)
        {
            if (_issued == Length)
            {
                throw new ChainExhaustedException("The scripted chain is exhausted.");
            }

            index = ++_issued;
        }

        Record(nameof(NextRoundAsync), index);
        return Task.FromResult(new RoundSeedCommitment(_chain, index, new Seed(_links[Length - index + 1])));
    }

    public Task<CrashPoint> GetCrashPointAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct)
    {
        Record(nameof(GetCrashPointAsync), roundIndex);
        EnsureIssued(chain, roundIndex);
        return Task.FromResult(new CrashPoint(_crashPoints[roundIndex - 1]));
    }

    public Task<RevealedRound> RevealAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct)
    {
        Record(nameof(RevealAsync), roundIndex);
        EnsureIssued(chain, roundIndex);
        return Task.FromResult(new RevealedRound(
            SeedOf(roundIndex),
            new Seed(_links[Length - roundIndex + 1]),
            _salt,
            new CrashPoint(_crashPoints[roundIndex - 1])));
    }

    public CrashPoint ComputeCrashPoint(Seed seed, Salt salt, Rtp rtp, Multiplier cap)
    {
        for (var r = 1; r <= Length; r++)
        {
            if (SeedOf(r) == seed)
            {
                return new CrashPoint(_crashPoints[r - 1]);
            }
        }

        throw new InvalidSeedException("Not a seed of the scripted chain.");
    }

    public VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(previousSeed);
        if (!SHA256.HashData(seed.AsSpan()).AsSpan().SequenceEqual(previousSeed.AsSpan()))
        {
            return VerificationResult.ChainMismatch;
        }

        return ComputeCrashPoint(seed, salt, rtp, cap) == claimed ? VerificationResult.Valid : VerificationResult.CrashPointMismatch;
    }

    private void EnsureIssued(ChainId chain, int roundIndex)
    {
        lock (_gate)
        {
            if (chain != _chain || roundIndex < 1 || roundIndex > _issued)
            {
                throw new RoundNotFoundException($"Round {roundIndex} was not issued.");
            }
        }
    }

    private void Record(string method, int roundIndex)
    {
        lock (_gate)
        {
            _calls.Add(new FairnessCall(method, roundIndex, _time.GetUtcNow()));
        }
    }
}
