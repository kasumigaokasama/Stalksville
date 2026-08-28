using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Intelligence.Engine;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class AlertStore(StalksvilleDbContext db) : IAlertStore
{
    public async Task<IReadOnlyList<Alert>> AddIfNewAsync(
        IReadOnlyList<AlertCandidate> candidates,
        EntityType entityType,
        Guid entityId,
        string entityTitle,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var keys = candidates.Select(c => c.DedupeKey).ToList();
        var existing = await db.Alerts
            .Where(a => keys.Contains(a.DedupeKey))
            .Select(a => a.DedupeKey)
            .ToListAsync(cancellationToken);

        var fresh = candidates
            .Where(c => !existing.Contains(c.DedupeKey))
            .Select(c => new Alert
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = entityId,
                EntityTitle = entityTitle,
                Kind = c.Kind,
                Severity = c.Severity,
                Title = c.Title,
                Body = c.Body,
                Evidence = c.EvidenceJson,
                DedupeKey = c.DedupeKey,
                CreatedAt = createdAt
            })
            .ToList();

        if (fresh.Count > 0)
        {
            db.Alerts.AddRange(fresh);
            await db.SaveChangesAsync(cancellationToken);
        }

        return fresh;
    }

    public async Task<IReadOnlyList<Alert>> ListAsync(AlertFilter filter, CancellationToken cancellationToken = default)
    {
        IQueryable<Alert> query = db.Alerts.AsNoTracking();

        if (filter.UnreadOnly == true)
        {
            query = query.Where(a => a.ReadAt == null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Kind))
        {
            query = query.Where(a => a.Kind == filter.Kind);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Clamp(filter.Limit, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(CancellationToken cancellationToken = default)
        => db.Alerts.AsNoTracking().CountAsync(a => a.ReadAt == null, cancellationToken);

    public async Task<bool> MarkReadAsync(Guid alertId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        var updated = await db.Alerts
            .Where(a => a.Id == alertId && a.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ReadAt, readAt), cancellationToken);
        return updated == 1;
    }

    public Task<int> MarkAllReadAsync(DateTimeOffset readAt, CancellationToken cancellationToken = default)
        => db.Alerts
            .Where(a => a.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ReadAt, readAt), cancellationToken);
}
