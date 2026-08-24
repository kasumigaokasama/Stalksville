using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

public sealed class InvestigationStore(StalksvilleDbContext db) : IInvestigationStore
{
    public async Task<Investigation> CreateAsync(string title, string? description, CancellationToken cancellationToken = default)
    {
        var investigation = new Investigation
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Status = InvestigationStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Investigations.Add(investigation);
        await db.SaveChangesAsync(cancellationToken);
        return investigation;
    }

    public async Task<IReadOnlyList<InvestigationListItem>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        var investigations = await db.Investigations
            .OrderByDescending(i => i.UpdatedAt)
            .ToListAsync(cancellationToken);

        if (investigations.Count == 0)
        {
            return [];
        }

        var ids = investigations.Select(i => i.Id).ToList();
        var targetCounts = await db.InvestigationTargets
            .Where(t => ids.Contains(t.InvestigationId))
            .GroupBy(t => t.InvestigationId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var noteCounts = await db.InvestigationNotes
            .Where(n => ids.Contains(n.InvestigationId))
            .GroupBy(n => n.InvestigationId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return investigations
            .Where(i => includeArchived || i.Status == InvestigationStatus.Active)
            .Select(i => new InvestigationListItem(
                i,
                targetCounts.GetValueOrDefault(i.Id),
                noteCounts.GetValueOrDefault(i.Id)))
            .ToList();
    }

    public Task<Investigation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Investigations.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<InvestigationTarget>> GetTargetsAsync(Guid investigationId, CancellationToken cancellationToken = default)
        => await db.InvestigationTargets
            .Where(t => t.InvestigationId == investigationId)
            .OrderBy(t => t.AddedAt)
            .ToListAsync(cancellationToken);

    public async Task<InvestigationTarget> AddTargetAsync(Guid investigationId, EntityType entityType, Guid entityId, string addedBy, CancellationToken cancellationToken = default)
    {
        var target = new InvestigationTarget
        {
            Id = Guid.NewGuid(),
            InvestigationId = investigationId,
            EntityType = entityType,
            EntityId = entityId,
            AddedAt = DateTimeOffset.UtcNow,
            AddedBy = addedBy
        };
        db.InvestigationTargets.Add(target);
        await TouchInvestigationAsync(investigationId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return target;
    }

    public async Task<bool> RemoveTargetAsync(Guid investigationId, Guid targetId, CancellationToken cancellationToken = default)
    {
        var removed = await db.InvestigationTargets
            .Where(t => t.InvestigationId == investigationId && t.Id == targetId)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
        {
            await TouchInvestigationAsync(investigationId, cancellationToken);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return removed > 0;
    }

    public async Task<IReadOnlyList<InvestigationNote>> GetNotesAsync(Guid investigationId, CancellationToken cancellationToken = default)
        => await db.InvestigationNotes
            .Where(n => n.InvestigationId == investigationId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<InvestigationNote> AddNoteAsync(Guid investigationId, string content, string author, CancellationToken cancellationToken = default)
    {
        var note = new InvestigationNote
        {
            Id = Guid.NewGuid(),
            InvestigationId = investigationId,
            Content = content,
            Author = author,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.InvestigationNotes.Add(note);
        await TouchInvestigationAsync(investigationId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return note;
    }

    public async Task SetStatusAsync(Guid investigationId, InvestigationStatus status, CancellationToken cancellationToken = default)
    {
        var investigation = await db.Investigations.FirstAsync(i => i.Id == investigationId, cancellationToken);
        investigation.Status = status;
        investigation.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task TouchInvestigationAsync(Guid investigationId, CancellationToken cancellationToken)
    {
        var investigation = await db.Investigations.FirstOrDefaultAsync(i => i.Id == investigationId, cancellationToken);
        if (investigation is not null)
        {
            investigation.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
