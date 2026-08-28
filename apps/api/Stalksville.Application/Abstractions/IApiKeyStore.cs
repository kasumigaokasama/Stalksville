using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

/// <summary>Persistence port for client API keys (hashed at rest, soft-revoked).</summary>
public interface IApiKeyStore
{
    Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>Resolves an active (non-revoked) key with its user by SHA-256 hash.</summary>
    Task<(ApiKey Key, User User)?> FindActiveByHashAsync(string keyHash, CancellationToken cancellationToken = default);

    Task TouchAsync(Guid apiKeyId, DateTimeOffset usedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKey>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Soft-revokes a key; false when it does not exist or is already revoked.</summary>
    Task<bool> RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken = default);
}
