namespace Stalksville.Application.Abstractions;

/// <summary>Cache port; in-memory today, Redis-ready. Values must be serializable by the provider.</summary>
public interface ICacheProvider
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, object value, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
