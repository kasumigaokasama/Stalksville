using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class ApiKeyStore(StalksvilleDbContext db) : IApiKeyStore
{
    public async Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(cancellationToken);
        return apiKey;
    }

    public async Task<(ApiKey Key, User User)?> FindActiveByHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        var match = await db.ApiKeys.AsNoTracking()
            .Where(k => k.KeyHash == keyHash && k.RevokedAt == null)
            .Join(db.Users.AsNoTracking(),
                k => k.UserId,
                u => u.Id,
                (k, u) => new { Key = k, User = u })
            .FirstOrDefaultAsync(cancellationToken);

        return match is null ? null : (match.Key, match.User);
    }

    public Task TouchAsync(Guid apiKeyId, DateTimeOffset usedAt, CancellationToken cancellationToken = default)
        => db.ApiKeys
            .Where(k => k.Id == apiKeyId)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, usedAt), cancellationToken);

    public async Task<IReadOnlyList<ApiKey>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await db.ApiKeys.AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        var updated = await db.ApiKeys
            .Where(k => k.Id == apiKeyId && k.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.RevokedAt, DateTimeOffset.UtcNow), cancellationToken);
        return updated == 1;
    }
}
