using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Domain;
using Xunit;

namespace Apogeu.Fairness.Tests.Mutation;

/// <summary>
/// UT-02 of SPEC-0016: <see cref="SeedChain"/> against an independent walk of the chain (repeated SHA256.HashData in the
/// test) and its guard clauses, so that the mutation run kills the mutants of the chain arithmetic and of the guards.
/// </summary>
public sealed class SeedChainMutationTests
{
    private const string Tag = "SPEC-0016:UT-02";

    [Fact(DisplayName = "Dada uma semente, quando Next é chamado, então devolve SHA256 dos 32 bytes e não altera a entrada")]
    [Trait("Spec", Tag)]
    public void GivenSeedWhenNextThenSha256OfTheBytes()
    {
        var origin = MutationFixtures.Origin();

        var next = SeedChain.Next(new Seed(origin));

        Assert.Equal(SHA256.HashData(origin), next.ToArray());
        Assert.NotEqual(origin, next.ToArray());
    }

    [Fact(DisplayName = "Dada uma semente nula, quando Next é chamado, então lança ArgumentNullException")]
    [Trait("Spec", Tag)]
    public void GivenNullSeedWhenNextThenArgumentNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => SeedChain.Next(null!));

        Assert.Equal("seed", exception.ParamName);
    }

    [Theory(DisplayName = "Dado um elo, quando Advance anda k passos, então devolve k aplicações de SHA256")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(SeedChain.CheckpointInterval)]
    [Trait("Spec", Tag)]
    public void GivenLinkWhenAdvancedKStepsThenKSha256Applications(int steps)
    {
        var origin = MutationFixtures.Origin();

        var reached = SeedChain.Advance(new Seed(origin), steps);

        Assert.Equal(MutationFixtures.HashTimes(origin, steps), reached.ToArray());
    }

    [Fact(DisplayName = "Dado um elo, quando Advance anda a e depois b passos, então chega ao mesmo elo que andando a + b")]
    [Trait("Spec", Tag)]
    public void GivenLinkWhenAdvancedInTwoHopsThenSameAsOneHop()
    {
        var origin = new Seed(MutationFixtures.Origin());

        var twoHops = SeedChain.Advance(SeedChain.Advance(origin, 3), 4);

        Assert.Equal(SeedChain.Advance(origin, 7), twoHops);
        Assert.Equal(SeedChain.Next(SeedChain.Advance(origin, 6)), twoHops);
    }

    [Fact(DisplayName = "Dado um elo nulo, quando Advance é chamado, então lança ArgumentNullException")]
    [Trait("Spec", Tag)]
    public void GivenNullLinkWhenAdvancedThenArgumentNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => SeedChain.Advance(null!, 1));

        Assert.Equal("link", exception.ParamName);
    }

    [Theory(DisplayName = "Dado um número de passos negativo, quando Advance é chamado, então lança ArgumentOutOfRangeException")]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [Trait("Spec", Tag)]
    public void GivenNegativeStepsWhenAdvancedThenArgumentOutOfRange(int steps)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => SeedChain.Advance(new Seed(MutationFixtures.Origin()), steps));

        Assert.Equal("steps", exception.ParamName);
    }

    [Theory(DisplayName = "Dado um comprimento N, quando Build percorre a cadeia, então o hash terminal é h_N e os checkpoints são h_k com k múltiplo de 10.000 e k < N")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(17)]
    [InlineData(SeedChain.CheckpointInterval - 1)]
    [InlineData(SeedChain.CheckpointInterval)]
    [InlineData(SeedChain.CheckpointInterval + 1)]
    [InlineData((2 * SeedChain.CheckpointInterval) + 1)]
    [Trait("Spec", Tag)]
    public void GivenLengthWhenBuiltThenTerminalAndCheckpointsMatchAnIndependentWalk(int length)
    {
        var origin = MutationFixtures.Origin();
        var links = MutationFixtures.Walk(origin, length);

        var layout = SeedChain.Build(new Seed(origin), length);

        Assert.Equal(links[length], layout.TerminalHash.ToArray());
        var expectedIndices = Enumerable.Range(0, length).Where(k => k % SeedChain.CheckpointInterval == 0).ToList();
        Assert.Equal(expectedIndices, layout.Checkpoints.Select(checkpoint => checkpoint.LinkIndex).ToList());
        foreach (var checkpoint in layout.Checkpoints)
        {
            Assert.Equal(links[checkpoint.LinkIndex], checkpoint.Link.ToArray());
        }
    }

    [Fact(DisplayName = "Dado N = 1, quando Build percorre a cadeia, então há só o checkpoint h_0 (a origem) e o terminal é SHA256(h_0)")]
    [Trait("Spec", Tag)]
    public void GivenLengthOneWhenBuiltThenOnlyOriginCheckpointAndTerminalIsItsHash()
    {
        var origin = MutationFixtures.Origin();

        var layout = SeedChain.Build(new Seed(origin), 1);

        var checkpoint = Assert.Single(layout.Checkpoints);
        Assert.Equal(0, checkpoint.LinkIndex);
        Assert.Equal(origin, checkpoint.Link.ToArray());
        Assert.Equal(SHA256.HashData(origin), layout.TerminalHash.ToArray());
    }

    [Fact(DisplayName = "Dada uma cadeia de 250.000 elos, quando Build percorre a cadeia, então há 25 checkpoints e cada um leva ao terminal com Advance")]
    [Trait("Spec", Tag)]
    public void GivenLongChainWhenBuiltThenTwentyFiveCheckpointsThatAdvanceToTheTerminal()
    {
        const int Length = 250_000;
        var origin = MutationFixtures.Origin();

        var layout = SeedChain.Build(new Seed(origin), Length);

        Assert.Equal(25, layout.Checkpoints.Count);
        Assert.Equal(
            Enumerable.Range(0, 25).Select(i => i * SeedChain.CheckpointInterval).ToList(),
            layout.Checkpoints.Select(checkpoint => checkpoint.LinkIndex).ToList());
        Assert.Equal(MutationFixtures.HashTimes(origin, Length), layout.TerminalHash.ToArray());
        var last = layout.Checkpoints[^1];
        Assert.Equal(layout.TerminalHash, SeedChain.Advance(last.Link, Length - last.LinkIndex));
        var middle = layout.Checkpoints[12];
        Assert.Equal(layout.Checkpoints[13].Link, SeedChain.Advance(middle.Link, SeedChain.CheckpointInterval));
    }

    [Fact(DisplayName = "Dada uma origem nula, quando Build é chamado, então lança ArgumentNullException")]
    [Trait("Spec", Tag)]
    public void GivenNullOriginWhenBuiltThenArgumentNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => SeedChain.Build(null!, 1));

        Assert.Equal("origin", exception.ParamName);
    }

    [Theory(DisplayName = "Dado um comprimento menor que 1, quando Build é chamado, então lança ArgumentOutOfRangeException")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [Trait("Spec", Tag)]
    public void GivenLengthBelowOneWhenBuiltThenArgumentOutOfRange(int length)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => SeedChain.Build(new Seed(MutationFixtures.Origin()), length));

        Assert.Equal("length", exception.ParamName);
    }

    [Theory(DisplayName = "Dados N e a rodada r em [1, N], quando LinkIndexOfRound é chamado, então devolve N − r")]
    [InlineData(1, 1, 0)]
    [InlineData(10, 1, 9)]
    [InlineData(10, 4, 6)]
    [InlineData(10, 10, 0)]
    [InlineData(1_000_000, 1, 999_999)]
    [InlineData(1_000_000, 1_000_000, 0)]
    [InlineData(25_000, 4_999, 20_001)]
    [Trait("Spec", Tag)]
    public void GivenRoundInRangeWhenLinkIndexOfRoundThenLengthMinusRound(int length, int round, int expected)
    {
        Assert.Equal(expected, SeedChain.LinkIndexOfRound(length, round));
    }

    [Fact(DisplayName = "Dada uma cadeia curta, quando LinkIndexOfRound aponta o elo da rodada r, então SHA256(s_r) = s_{r−1} e s_0 é o terminal")]
    [Trait("Spec", Tag)]
    public void GivenShortChainWhenLinkIndexOfRoundThenRoundSeedsChainBackToTheTerminal()
    {
        const int Length = 12;
        var links = MutationFixtures.Walk(MutationFixtures.Origin(), Length);

        for (var round = 1; round <= Length; round++)
        {
            var seed = links[SeedChain.LinkIndexOfRound(Length, round)];
            var previous = round == 1 ? links[Length] : links[SeedChain.LinkIndexOfRound(Length, round - 1)];
            Assert.Equal(previous, SHA256.HashData(seed));
        }
    }

    [Theory(DisplayName = "Dada uma rodada fora de [1, N], quando LinkIndexOfRound é chamado, então lança ArgumentOutOfRangeException")]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    [InlineData(10, 11)]
    [InlineData(1, 2)]
    [InlineData(10, int.MaxValue)]
    [Trait("Spec", Tag)]
    public void GivenRoundOutOfRangeWhenLinkIndexOfRoundThenArgumentOutOfRange(int length, int round)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => SeedChain.LinkIndexOfRound(length, round));

        Assert.Equal("round", exception.ParamName);
    }

    [Theory(DisplayName = "Dado o índice de um elo, quando CheckpointOf é chamado, então devolve o maior múltiplo de 10.000 que não o excede")]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(9_999, 0)]
    [InlineData(10_000, 10_000)]
    [InlineData(10_001, 10_000)]
    [InlineData(12_345, 10_000)]
    [InlineData(29_999, 20_000)]
    [InlineData(999_999, 990_000)]
    [InlineData(int.MaxValue, 2_147_480_000)]
    [Trait("Spec", Tag)]
    public void GivenLinkIndexWhenCheckpointOfThenLargestMultipleNotAbove(int linkIndex, int expected)
    {
        Assert.Equal(expected, SeedChain.CheckpointOf(linkIndex));
    }

    [Fact(DisplayName = "Dada uma cadeia construída, quando o elo h_k é alcançado do seu checkpoint com Advance, então é o h_k da caminhada independente")]
    [Trait("Spec", Tag)]
    public void GivenBuiltChainWhenLinkReachedFromItsCheckpointThenItIsTheIndependentLink()
    {
        const int Length = (2 * SeedChain.CheckpointInterval) + 5;
        var origin = MutationFixtures.Origin();
        var links = MutationFixtures.Walk(origin, Length);
        var layout = SeedChain.Build(new Seed(origin), Length);

        foreach (var linkIndex in new[] { 0, 1, 9_999, 10_000, 10_001, 19_999, 20_000, Length - 1 })
        {
            var checkpointIndex = SeedChain.CheckpointOf(linkIndex);
            var checkpoint = Assert.Single(layout.Checkpoints, candidate => candidate.LinkIndex == checkpointIndex);

            var reached = SeedChain.Advance(checkpoint.Link, linkIndex - checkpointIndex);

            Assert.Equal(links[linkIndex], reached.ToArray());
        }
    }

    [Theory(DisplayName = "Dado um índice negativo, quando CheckpointOf é chamado, então lança ArgumentOutOfRangeException")]
    [InlineData(-1)]
    [InlineData(-10_000)]
    [InlineData(int.MinValue)]
    [Trait("Spec", Tag)]
    public void GivenNegativeLinkIndexWhenCheckpointOfThenArgumentOutOfRange(int linkIndex)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => SeedChain.CheckpointOf(linkIndex));

        Assert.Equal("linkIndex", exception.ParamName);
    }

    [Theory(DisplayName = "Dados bytes de tamanho diferente de 32, quando um Seed é criado, então lança InvalidSeedException")]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    [Trait("Spec", Tag)]
    public void GivenBytesNotThirtyTwoLongWhenSeedCreatedThenInvalidSeed(int length)
    {
        Assert.Throws<InvalidSeedException>(() => new Seed(new byte[length]));
    }
}
