using System.Security.Cryptography;
using Apogeu.Fairness.Contracts;
using Apogeu.Fairness.Tests.Support;
using Apogeu.TestKit.Containers;
using Xunit;

namespace Apogeu.Fairness.Tests.Integration;

/// <summary>
/// SPEC-0065 IT-01: the chain links encrypted with the Host's Data Protection stay readable after a restart with the same
/// key directory (<c>DataProtection:KeysPath</c>, the volume <c>dataprotection-keys</c> in production).
/// </summary>
[Collection(FairnessHostDependencies.Name)]
public sealed class DataProtectionKeysPathTests(PostgreSqlContainerFixture postgres, RedisContainerFixture redis)
{
    private const int Length = 100;
    private static readonly Rtp DemoRtp = new(0.97m);
    private static readonly Multiplier DefaultCap = new(10000.00m);

    [Fact(DisplayName = "Dado o Host com DataProtection:KeysPath num diretório, quando uma cadeia de sementes é cifrada, o Host para e sobe de novo com o mesmo diretório, então a semente é decifrada")]
    [Trait("Spec", "SPEC-0065:IT-01")]
    [Trait("Category", "Integration")]
    public async Task GivenHostWithKeysPathWhenChainIsEncryptedAndHostRestartsWithSameDirectoryThenSeedIsDecrypted()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await TestDatabase.CreateAsync(postgres.ConnectionString, ct);
        using var keys = new TemporaryKeyDirectory();
        var sala = new SalaId(Guid.NewGuid());
        ChainInfo chain;
        RoundSeedCommitment first;

        await using (var host = await FairnessApiHost.StartAsync(database, redis.ConnectionString, keys.Directory))
        {
            chain = await host.RunAsync(fairness => fairness.CreateChainAsync(sala, Length, ct));
            first = await host.RunAsync(fairness => fairness.NextRoundAsync(sala, ct));
        }

        // The key ring that encrypted the chain is in the configured directory (the volume), not elsewhere.
        var keyFiles = keys.Directory.GetFiles("key-*.xml");
        Assert.True(
            keyFiles.Length > 0,
            $"The Host wrote no Data Protection key to DataProtection:KeysPath ({keys.Directory.FullName}): the key ring is not persisted there.");

        await using (var restarted = await FairnessApiHost.StartAsync(database, redis.ConnectionString, keys.Directory))
        {
            var revealed = await restarted.RunAsync(fairness => fairness.RevealAsync(chain.ChainId, first.RoundIndex, DemoRtp, DefaultCap, ct));

            Assert.Equal(first.Commitment.ToArray(), SHA256.HashData(revealed.Seed.ToArray()));
        }

        // Control: the seed is readable because of that directory; with another (empty) one the same chain is not.
        using var otherKeys = new TemporaryKeyDirectory();
        await using var stranger = await FairnessApiHost.StartAsync(database, redis.ConnectionString, otherKeys.Directory);
        await Assert.ThrowsAnyAsync<Exception>(
            () => stranger.RunAsync(fairness => fairness.RevealAsync(chain.ChainId, first.RoundIndex, DemoRtp, DefaultCap, ct)));
    }
}
