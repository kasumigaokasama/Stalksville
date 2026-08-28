using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Stalksville.Application.Abstractions;

namespace Stalksville.Infrastructure.Caching;

/// <summary>
/// Redis-backed ICacheProvider (shared between API and worker instances). Values are stored as
/// System.Text.Json strings with the configured per-key TTLs; a Redis outage degrades to
/// cache misses rather than failing requests.
/// </summary>
public sealed class RedisCacheProvider(IConnectionMultiplexer redis, ILogger<RedisCacheProvider> logger) : ICacheProvider
{
    private static readonly JsonSerializerOptions SerializeOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = redis.GetDatabase();
            var value = await database.StringGetAsync(key);
            if (value.IsNull)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>((string)value!, SerializeOptions);
        }
        catch (RedisException ex)
        {
            // A cache must never take the request path down.
            logger.LogWarning(ex, "Redis cache read failed for {Key}; treating as a miss", key);
            return default;
        }
    }

    public async Task SetAsync(string key, object value, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = redis.GetDatabase();
            await database.StringSetAsync(key, JsonSerializer.Serialize(value, SerializeOptions), timeToLive);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis cache write failed for {Key}; continuing without caching", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var database = redis.GetDatabase();
            await database.KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis cache delete failed for {Key}", key);
        }
    }
}
