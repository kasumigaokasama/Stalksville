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

    public async Task<IReadOnlyList<AlertWithRead>> ListAsync(AlertFilter filter, Guid userId, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(db.Alerts.AsNoTracking().AsQueryable(), filter);
        if (filter.UnreadOnly == true)
        {
            query = query.Where(a => !db.AlertReads.Any(r => r.AlertId == a.Id && r.UserId == userId));
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(Math.Max(0, filter.Offset))
            .Take(Math.Clamp(filter.Limit, 1, 500))
            .Select(a => new AlertWithRead(
                a,
                db.AlertReads.Where(r => r.AlertId == a.Id && r.UserId == userId).Select(r => (DateTimeOffset?)r.ReadAt).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(AlertFilter filter, Guid userId, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(db.Alerts.AsNoTracking().AsQueryable(), filter);
        if (filter.UnreadOnly == true)
        {
            query = query.Where(a => !db.AlertReads.Any(r => r.AlertId == a.Id && r.UserId == userId));
        }

        return query.CountAsync(cancellationToken);
    }

    private static IQueryable<Alert> ApplyFilter(IQueryable<Alert> query, AlertFilter filter)
    {
        // Unread is user-relative (resolved through AlertReads by the caller), not the legacy column.
        if (!string.IsNullOrWhiteSpace(filter.Kind))
        {
            query = query.Where(a => a.Kind == filter.Kind);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        return query;
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        => db.Alerts.AsNoTracking()
            .CountAsync(a => !db.AlertReads.Any(r => r.AlertId == a.Id && r.UserId == userId), cancellationToken);

    public async Task<bool> MarkReadAsync(Guid alertId, Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        var already = await db.AlertReads.AsNoTracking().AnyAsync(r => r.AlertId == alertId && r.UserId == userId, cancellationToken);
        if (already || !await db.Alerts.AsNoTracking().AnyAsync(a => a.Id == alertId, cancellationToken))
        {
            return false;
        }

        db.AlertReads.Add(new AlertRead { AlertId = alertId, UserId = userId, ReadAt = readAt });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken cancellationToken = default)
    {
        var unreadIds = await db.Alerts.AsNoTracking()
            .Where(a => !db.AlertReads.Any(r => r.AlertId == a.Id && r.UserId == userId))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (unreadIds.Count == 0)
        {
            return 0;
        }

        db.AlertReads.AddRange(unreadIds.Select(id => new AlertRead { AlertId = id, UserId = userId, ReadAt = readAt }));
        await db.SaveChangesAsync(cancellationToken);
        return unreadIds.Count;
    }
}
