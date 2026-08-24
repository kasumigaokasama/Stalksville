using Microsoft.Extensions.Caching.Memory;
using Stalksville.Application.Abstractions;

namespace Stalksville.Infrastructure.Caching;

/// <summary>In-memory implementation; swap for a Redis provider later without touching consumers.</summary>
public sealed class MemoryCacheProvider(IMemoryCache memory) : ICacheProvider
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(memory.TryGetValue(key, out var value) && value is T typed ? typed : default);
    }

    public Task SetAsync(string key, object value, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        memory.Set(key, value, timeToLive);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        memory.Remove(key);
        return Task.CompletedTask;
    }
}
