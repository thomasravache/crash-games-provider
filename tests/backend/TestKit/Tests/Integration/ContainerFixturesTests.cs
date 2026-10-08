using Apogeu.TestKit.Containers;
using Npgsql;
using StackExchange.Redis;
using Xunit;

namespace Apogeu.TestKit.Tests.Integration;

public sealed class ContainerFixturesTests
{
    private static readonly TimeSpan AfterDisposalTimeout = TimeSpan.FromSeconds(5);

    [Fact(DisplayName = "Dadas as fixtures Testcontainers, quando um teste executa SELECT 1 no PostgreSQL, então responde e o container é descartado ao fim")]
    [Trait("Spec", "SPEC-0003:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenPostgreSqlFixtureWhenSelectOneRunsThenItAnswersAndContainerIsDisposed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixture = new PostgreSqlContainerFixture();
        string connectionString;

        await fixture.InitializeAsync();
        try
        {
            connectionString = fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "The PostgreSQL fixture exposes no connection string.");

            await using var connection = new NpgsqlConnection(WithoutPooling(connectionString, timeoutSeconds: 30));
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);

            Assert.Equal(1, Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await fixture.DisposeAsync();
        }

        // After disposal the container is gone: the same server no longer accepts connections.
        await using var afterDisposal = new NpgsqlConnection(
            WithoutPooling(connectionString, timeoutSeconds: (int)AfterDisposalTimeout.TotalSeconds));
        await Assert.ThrowsAnyAsync<Exception>(() => afterDisposal.OpenAsync(cancellationToken));
    }

    [Fact(DisplayName = "Dadas as fixtures Testcontainers, quando um teste executa PING no Redis, então responde e o container é descartado ao fim")]
    [Trait("Spec", "SPEC-0003:IT-02")]
    [Trait("Category", "Integration")]
    public async Task GivenRedisFixtureWhenPingRunsThenItAnswersAndContainerIsDisposed()
    {
        var fixture = new RedisContainerFixture();
        string connectionString;

        await fixture.InitializeAsync();
        try
        {
            connectionString = fixture.ConnectionString;
            Assert.False(string.IsNullOrWhiteSpace(connectionString), "The Redis fixture exposes no connection string.");

            await using var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
            var latency = await redis.GetDatabase().PingAsync();

            Assert.True(redis.IsConnected, "Expected the Redis connection to be open after PING.");
            Assert.True(latency >= TimeSpan.Zero, "Expected PING to return a round-trip time.");
        }
        finally
        {
            await fixture.DisposeAsync();
        }

        // After disposal the container is gone: connecting to the same server fails.
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = true;
        options.ConnectRetry = 0;
        options.ConnectTimeout = (int)AfterDisposalTimeout.TotalMilliseconds;
        await Assert.ThrowsAsync<RedisConnectionException>(() => ConnectionMultiplexer.ConnectAsync(options));
    }

    private static string WithoutPooling(string connectionString, int timeoutSeconds) =>
        new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false, Timeout = timeoutSeconds }.ConnectionString;
}
