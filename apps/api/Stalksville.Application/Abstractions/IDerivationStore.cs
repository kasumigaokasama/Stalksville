using Stalksville.Domain.Entities;

namespace Stalksville.Application.Abstractions;

/// <summary>Filter for timeline queries; nulls mean "no restriction".</summary>
public sealed record TimelineFilter(
    EntityType? EntityType = null,
    Guid? EntityId = null,
    string? EventType = null,
    bool? IsDerived = null,
    int Limit = 100);

/// <summary>Persistence port for derived intelligence: relationships, evidence and timeline events.</summary>
public interface IDerivationStore
{
    /// <summary>Keeps the player→clan relationship row in sync with a membership transition.</summary>
    Task SyncMembershipRelationshipAsync(Guid playerId, Guid clanId, bool current, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Relationship>> GetRelationshipsAsync(EntityType entityType, Guid entityId, CancellationToken cancellationToken = default);

    Task AddTimelineRangeAsync(IReadOnlyList<TimelineEvent> events, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(EntityType entityType, Guid entityId, int limit = 100, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimelineEvent>> GetRecentTimelineAsync(int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimelineEvent>> GetFilteredTimelineAsync(TimelineFilter filter, CancellationToken cancellationToken = default);

    Task AddEvidenceRangeAsync(IReadOnlyList<Evidence> evidence, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Evidence>>> GetEvidenceForAsync(string entityType, IReadOnlyList<Guid> entityIds, CancellationToken cancellationToken = default);

    Task<int> CountRelationshipsAsync(CancellationToken cancellationToken = default);

    /// <summary>All relationships (graph building and path finding).</summary>
    Task<IReadOnlyList<Relationship>> GetAllRelationshipsAsync(CancellationToken cancellationToken = default);
}
