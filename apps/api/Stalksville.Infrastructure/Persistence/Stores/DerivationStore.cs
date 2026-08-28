using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class DerivationStore(StalksvilleDbContext db) : IDerivationStore
{
    public async Task SyncMembershipRelationshipAsync(Guid playerId, Guid clanId, bool current, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var relationship = await db.Relationships.FirstOrDefaultAsync(r =>
            r.SourceEntityType == EntityType.Player &&
            r.SourceEntityId == playerId &&
            r.TargetEntityType == EntityType.Clan &&
            r.TargetEntityId == clanId, cancellationToken);

        if (relationship is null)
        {
            db.Relationships.Add(new Relationship
            {
                Id = Guid.NewGuid(),
                SourceEntityType = EntityType.Player,
                SourceEntityId = playerId,
                TargetEntityType = EntityType.Clan,
                TargetEntityId = clanId,
                Type = current ? RelationshipType.MemberOf : RelationshipType.PreviouslyMemberOf,
                Confidence = 1.0,
                FirstObservedAt = at,
                LastObservedAt = at,
                IsCurrent = current
            });
        }
        else
        {
            relationship.Type = current ? RelationshipType.MemberOf : RelationshipType.PreviouslyMemberOf;
            relationship.IsCurrent = current;
            relationship.LastObservedAt = at;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Relationship>> GetRelationshipsAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken = default)
        => await db.Relationships
            .Where(r => (r.SourceEntityType == entityType && r.SourceEntityId == entityId)
                     || (r.TargetEntityType == entityType && r.TargetEntityId == entityId))
            .OrderByDescending(r => r.IsCurrent)
            .ThenByDescending(r => r.LastObservedAt)
            .ToListAsync(cancellationToken);

    public async Task AddTimelineRangeAsync(IReadOnlyList<TimelineEvent> events, CancellationToken cancellationToken = default)
    {
        db.TimelineEvents.AddRange(events);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(EntityType entityType, Guid entityId, int limit = 100, CancellationToken cancellationToken = default)
        => await db.TimelineEvents
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .OrderByDescending(t => t.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TimelineEvent>> GetRecentTimelineAsync(int limit = 20, CancellationToken cancellationToken = default)
        => await db.TimelineEvents
            .OrderByDescending(t => t.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TimelineEvent>> GetFilteredTimelineAsync(TimelineFilter filter, CancellationToken cancellationToken = default)
    {
        var query = ApplyTimelineFilter(db.TimelineEvents.AsNoTracking().AsQueryable(), filter);

        return await query
            .OrderByDescending(t => t.OccurredAt)
            .Skip(Math.Max(0, filter.Offset))
            .Take(Math.Clamp(filter.Limit, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountTimelineAsync(TimelineFilter filter, CancellationToken cancellationToken = default)
        => ApplyTimelineFilter(db.TimelineEvents.AsNoTracking().AsQueryable(), filter).CountAsync(cancellationToken);

    private static IQueryable<TimelineEvent> ApplyTimelineFilter(IQueryable<TimelineEvent> query, TimelineFilter filter)
    {
        if (filter.EntityType is { } entityType)
        {
            query = query.Where(t => t.EntityType == entityType);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(t => t.EntityId == entityId);
        }

        if (!string.IsNullOrEmpty(filter.EventType))
        {
            query = query.Where(t => t.EventType == filter.EventType);
        }

        if (filter.IsDerived is { } isDerived)
        {
            query = query.Where(t => t.IsDerived == isDerived);
        }

        return query;
    }

    public async Task AddEvidenceRangeAsync(IReadOnlyList<Evidence> evidence, CancellationToken cancellationToken = default)
    {
        db.Evidence.AddRange(evidence);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Evidence>>> GetEvidenceForAsync(string entityType, IReadOnlyList<Guid> entityIds, CancellationToken cancellationToken = default)
    {
        var rows = await db.Evidence
            .Where(e => e.EntityType == entityType && entityIds.Contains(e.EntityId))
            .OrderBy(e => e.CapturedAt)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(e => e.EntityId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Evidence>)g.ToList());
    }

    public Task<int> CountRelationshipsAsync(CancellationToken cancellationToken = default)
        => db.Relationships.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Relationship>> GetAllRelationshipsAsync(CancellationToken cancellationToken = default)
        => await db.Relationships
            .OrderByDescending(r => r.IsCurrent)
            .ToListAsync(cancellationToken);
}
