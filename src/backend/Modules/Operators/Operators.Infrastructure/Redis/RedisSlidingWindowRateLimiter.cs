using StackExchange.Redis;

namespace Apogeu.Operators.Infrastructure.Redis;

/// <summary>
/// Sliding-log limiter: one sorted set per partition, scored by the caller's instant in milliseconds. A Lua script drops
/// the permits older than the window, counts the rest and adds the new one only when under the limit, atomically.
/// </summary>
internal sealed class RedisSlidingWindowRateLimiter(OperatorsRedis redis) : IRateLimiter
{
    private const string Prefix = OperatorsRedis.KeyPrefix + "rate:";

    private const string Script = """
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
        if redis.call('ZCARD', KEYS[1]) >= tonumber(ARGV[3]) then
          return 0
        end
        redis.call('ZADD', KEYS[1], ARGV[2], ARGV[4])
        redis.call('PEXPIRE', KEYS[1], ARGV[5])
        return 1
        """;

    public async Task<bool> TryAcquireAsync(string partition, int permitLimit, TimeSpan window, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(partition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitLimit);

        var nowMs = now.ToUnixTimeMilliseconds();
        var windowMs = (long)window.TotalMilliseconds;
        var database = await redis.DatabaseAsync().ConfigureAwait(false);
        var result = await database.ScriptEvaluateAsync(
            Script,
            [(RedisKey)(Prefix + partition)],
            [nowMs - windowMs, nowMs, permitLimit, Guid.NewGuid().ToString("N"), windowMs * 2]).ConfigureAwait(false);
        return (long)result == 1;
    }
}
