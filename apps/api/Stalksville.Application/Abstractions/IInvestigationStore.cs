using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

public sealed record InvestigationListItem(Investigation Investigation, int TargetCount, int NoteCount);

/// <summary>Persistence port for investigations, their targets and notes.</summary>
public interface IInvestigationStore
{
    Task<Investigation> CreateAsync(string title, string? description, CancellationToken cancellationToken = default);

    /// <summary>Persists edits to a loaded investigation (title/description/assignee/tags).</summary>
    Task UpdateAsync(Investigation investigation, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvestigationListItem>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default);

    Task<Investigation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvestigationTarget>> GetTargetsAsync(Guid investigationId, CancellationToken cancellationToken = default);

    Task<InvestigationTarget> AddTargetAsync(Guid investigationId, EntityType entityType, Guid entityId, string addedBy, CancellationToken cancellationToken = default);

    Task<bool> RemoveTargetAsync(Guid investigationId, Guid targetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvestigationNote>> GetNotesAsync(Guid investigationId, CancellationToken cancellationToken = default);

    Task<InvestigationNote> AddNoteAsync(Guid investigationId, string content, string author, CancellationToken cancellationToken = default);

    Task SetStatusAsync(Guid investigationId, InvestigationStatus status, CancellationToken cancellationToken = default);
}
