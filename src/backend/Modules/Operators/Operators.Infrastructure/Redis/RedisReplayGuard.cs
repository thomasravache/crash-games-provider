using StackExchange.Redis;

namespace Apogeu.Operators.Infrastructure.Redis;

internal sealed class RedisReplayGuard(OperatorsRedis redis) : IReplayGuard
{
    private const string Prefix = OperatorsRedis.KeyPrefix + "replay:";

    public async Task<bool> TryRegisterAsync(string fingerprint, TimeSpan retention, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(fingerprint);

        var database = await redis.DatabaseAsync().ConfigureAwait(false);
        return await database.StringSetAsync(Prefix + fingerprint, 1, retention, When.NotExists).ConfigureAwait(false);
    }
}
