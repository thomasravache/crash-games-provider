using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Xunit;

namespace Apogeu.Fairness.Tests.Mutation;

/// <summary>
/// UT-02 of SPEC-0016: <see cref="FairnessVerifier"/> (SPEC-0007 §6) — the three outcomes, with the chain checked before
/// the crash point, and its guard clauses (null arguments, InvalidRtp, InvalidCap).
/// </summary>
public sealed class FairnessVerifierMutationTests
{
    private const string Tag = "SPEC-0016:UT-02";

    private static readonly Rtp Rtp97 = new(0.97m);
    private static readonly Multiplier Cap = new(10000.00m);

    private readonly byte[] _seed = MutationFixtures.Origin();
    private readonly Salt _salt = new(MutationFixtures.SaltBytes());

    private Seed SeedOfRound => new(_seed);

    private Seed PreviousSeed => new(SHA256.HashData(_seed));

    private CrashPoint Crash => CrashPointCalculator.Compute(SeedOfRound, _salt, Rtp97, Cap);

    [Fact(DisplayName = "Dados s_r, SHA256(s_r) e o crash calculado, quando Verify é chamado, então devolve Valid")]
    [Trait("Spec", Tag)]
    public void GivenChainedSeedAndComputedCrashWhenVerifiedThenValid()
    {
        Assert.Equal(VerificationResult.Valid, FairnessVerifier.Verify(SeedOfRound, PreviousSeed, _salt, Rtp97, Cap, Crash));
    }

    [Fact(DisplayName = "Dado um crash informado diferente em 0,01, quando Verify é chamado, então devolve CrashPointMismatch")]
    [Trait("Spec", Tag)]
    public void GivenClaimedCrashOffByOneHundredthWhenVerifiedThenCrashPointMismatch()
    {
        var claimed = new CrashPoint(Crash.Value + 0.01m);

        Assert.Equal(VerificationResult.CrashPointMismatch, FairnessVerifier.Verify(SeedOfRound, PreviousSeed, _salt, Rtp97, Cap, claimed));
    }

    [Fact(DisplayName = "Dados s_r e um s_{r−1} que não é SHA256(s_r), quando Verify é chamado, então devolve ChainMismatch mesmo com o crash certo")]
    [Trait("Spec", Tag)]
    public void GivenUnchainedPreviousSeedWhenVerifiedThenChainMismatch()
    {
        var wrongPrevious = new Seed(MutationFixtures.OtherSeed());

        Assert.Equal(VerificationResult.ChainMismatch, FairnessVerifier.Verify(SeedOfRound, wrongPrevious, _salt, Rtp97, Cap, Crash));
        Assert.Equal(VerificationResult.ChainMismatch, FairnessVerifier.Verify(PreviousSeed, SeedOfRound, _salt, Rtp97, Cap, Crash));
        Assert.Equal(VerificationResult.ChainMismatch, FairnessVerifier.Verify(SeedOfRound, SeedOfRound, _salt, Rtp97, Cap, Crash));
    }

    [Fact(DisplayName = "Dados um s_{r−1} errado e um crash errado, quando Verify é chamado, então a cadeia é checada primeiro: ChainMismatch")]
    [Trait("Spec", Tag)]
    public void GivenWrongChainAndWrongCrashWhenVerifiedThenChainMismatch()
    {
        var wrongPrevious = new Seed(MutationFixtures.OtherSeed());
        var claimed = new CrashPoint(Crash.Value + 0.01m);

        Assert.Equal(VerificationResult.ChainMismatch, FairnessVerifier.Verify(SeedOfRound, wrongPrevious, _salt, Rtp97, Cap, claimed));
    }

    [Fact(DisplayName = "Dada uma seed nula, quando Verify é chamado, então lança ArgumentNullException")]
    [Trait("Spec", Tag)]
    public void GivenNullSeedWhenVerifiedThenArgumentNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => FairnessVerifier.Verify(null!, PreviousSeed, _salt, Rtp97, Cap, Crash));

        Assert.Equal("seed", exception.ParamName);
    }

    [Fact(DisplayName = "Dada uma previousSeed nula, quando Verify é chamado, então lança ArgumentNullException")]
    [Trait("Spec", Tag)]
    public void GivenNullPreviousSeedWhenVerifiedThenArgumentNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => FairnessVerifier.Verify(SeedOfRound, null!, _salt, Rtp97, Cap, Crash));

        Assert.Equal("previousSeed", exception.ParamName);
    }

    [Fact(DisplayName = "Dada uma salt nula, quando Verify é chamado (mesmo com a cadeia errada), então lança ArgumentNullException")]
    [Trait("Spec", Tag)]
    public void GivenNullSaltWhenVerifiedThenArgumentNull()
    {
        var wrongPrevious = new Seed(MutationFixtures.OtherSeed());

        Assert.Equal("salt", Assert.Throws<ArgumentNullException>(() => FairnessVerifier.Verify(SeedOfRound, PreviousSeed, null!, Rtp97, Cap, Crash)).ParamName);
        Assert.Equal("salt", Assert.Throws<ArgumentNullException>(() => FairnessVerifier.Verify(SeedOfRound, wrongPrevious, null!, Rtp97, Cap, Crash)).ParamName);
    }

    [Fact(DisplayName = "Dada a cadeia certa e um cap abaixo de 1.00, quando Verify é chamado, então lança InvalidCapException")]
    [Trait("Spec", Tag)]
    public void GivenCapBelowOneWhenVerifiedThenInvalidCap()
    {
        Assert.Throws<InvalidCapException>(() => FairnessVerifier.Verify(SeedOfRound, PreviousSeed, _salt, Rtp97, new Multiplier(0.99m), Crash));
    }

    [Fact(DisplayName = "Dada a cadeia certa e um RTP padrão, quando Verify é chamado, então lança InvalidRtpException")]
    [Trait("Spec", Tag)]
    public void GivenDefaultRtpWhenVerifiedThenInvalidRtp()
    {
        Assert.Throws<InvalidRtpException>(() => FairnessVerifier.Verify(SeedOfRound, PreviousSeed, _salt, default, Cap, Crash));
    }
}
