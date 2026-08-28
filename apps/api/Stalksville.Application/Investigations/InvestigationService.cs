using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Investigations;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;

namespace Stalksville.Application.Investigations;

/// <summary>
/// Investigation orchestration: cases, targets, notes and the aggregated per-case timeline and
/// stats. Cases are archived rather than deleted; targets must reference tracked entities.
/// </summary>
public sealed class InvestigationService(
    IInvestigationStore investigations,
    IPlayerStore players,
    IClanStore clans,
    IDerivationStore derivations,
    IAuditLog audit,
    ILogger<InvestigationService> logger)
{
    public async Task<InvestigationWorkspaceDto> CreateAsync(string title, string? description, string actor, CancellationToken cancellationToken = default)
    {
        var trimmed = title.Trim();
        if (trimmed.Length is < 3 or > 160)
        {
            throw new ArgumentException("Title must be between 3 and 160 characters.");
        }

        var investigation = await investigations.CreateAsync(trimmed, description?.Trim(), cancellationToken);
        await audit.WriteAsync(AuditActions.InvestigationCreated, $"investigation:{investigation.CaseNumber}",
            new { investigation.Title }, cancellationToken);
        logger.LogInformation("Investigation #{CaseNumber} created: {Title}", investigation.CaseNumber, trimmed);

        return await GetWorkspaceAsync(investigation.Id, cancellationToken);
    }

    public async Task<InvestigationWorkspaceDto> UpdateAsync(
        Guid investigationId,
        string? title,
        string? description,
        Guid? assignedToUserId,
        bool assigneeProvided,
        IReadOnlyList<string>? tags,
        CancellationToken cancellationToken = default)
    {
        var investigation = await investigations.FindByIdAsync(investigationId, cancellationToken)
            ?? throw new EntityNotFoundException("investigation", investigationId);

        if (title is not null)
        {
            var trimmed = title.Trim();
            if (trimmed.Length is < 3 or > 160)
            {
                throw new ArgumentException("Title must be between 3 and 160 characters.");
            }

            investigation.Title = trimmed;
        }

        if (description is not null)
        {
            investigation.Description = description.Trim() is { Length: > 0 } d ? d : null;
        }

        if (assigneeProvided)
        {
            investigation.AssignedToUserId = assignedToUserId;
        }

        if (tags is not null)
        {
            investigation.Tags = tags
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length is > 0 and <= 32)
                .Distinct(StringComparer.Ordinal)
                .Take(12)
                .ToList();
        }

        await investigations.UpdateAsync(investigation, cancellationToken);
        await audit.WriteAsync(AuditActions.InvestigationUpdated, $"investigation:{investigation.CaseNumber}",
            new { investigation.Title, investigation.AssignedToUserId, investigation.Tags }, cancellationToken);

        return await GetWorkspaceAsync(investigation.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<InvestigationSummaryDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        var items = await investigations.ListAsync(includeArchived, cancellationToken);
        return items.Select(i => ToSummary(i.Investigation, i.TargetCount, i.NoteCount)).ToList();
    }

    public async Task<InvestigationWorkspaceDto> GetWorkspaceAsync(Guid investigationId, CancellationToken cancellationToken = default)
    {
        var investigation = await investigations.FindByIdAsync(investigationId, cancellationToken)
            ?? throw new EntityNotFoundException("investigation", investigationId);

        var targets = await investigations.GetTargetsAsync(investigation.Id, cancellationToken);
        var notes = await investigations.GetNotesAsync(investigation.Id, cancellationToken);

        var targetDtos = new List<InvestigationTargetDto>();
        var timeline = new List<Models.TimelineEventDto>();
        var snapshotTotal = 0;
        var highConfidence = 0;

        foreach (var target in targets)
        {
            var (displayName, snapshotCount) = target.EntityType switch
            {
                EntityType.Player => await PlayerDetailsAsync(target.EntityId, cancellationToken),
                EntityType.Clan => await ClanDetailsAsync(target.EntityId, cancellationToken),
                _ => ("unknown entity", 0)
            };

            var relationships = await derivations.GetRelationshipsAsync(target.EntityType, target.EntityId, cancellationToken);
            var currentRelationships = relationships.Count(r => r.IsCurrent);
            highConfidence += relationships.Count(r => r.Confidence >= 0.9);

            targetDtos.Add(new InvestigationTargetDto(
                target.Id,
                target.EntityType.ToString().ToLowerInvariant(),
                target.EntityId,
                displayName,
                target.AddedAt,
                target.AddedBy,
                snapshotCount,
                currentRelationships));

            var events = await derivations.GetTimelineAsync(target.EntityType, target.EntityId, limit: 200, cancellationToken);
            timeline.AddRange(events.Select(ToTimelineDto));
            snapshotTotal += snapshotCount;
        }

        timeline.Sort((a, b) => b.OccurredAt.CompareTo(a.OccurredAt));

        return new InvestigationWorkspaceDto(
            ToSummary(investigation, targets.Count, notes.Count),
            new InvestigationStatsDto(targets.Count, timeline.Count, snapshotTotal, highConfidence),
            targetDtos,
            notes.Select(n => new InvestigationNoteDto(n.Id, n.Content, n.Author, n.CreatedAt)).ToList(),
            timeline.Take(60).ToList());
    }

    public async Task<InvestigationWorkspaceDto> AddTargetAsync(Guid investigationId, string entityType, Guid entityId, string actor, CancellationToken cancellationToken = default)
    {
        var investigation = await investigations.FindByIdAsync(investigationId, cancellationToken)
            ?? throw new EntityNotFoundException("investigation", investigationId);

        if (investigation.Status == InvestigationStatus.Archived)
        {
            throw new InvalidOperationException("Archived investigations cannot be modified.");
        }

        var parsed = ParseEntityType(entityType);

        // Targets must reference entities Stalksville already tracks — an investigation never
        // invents entities, it organizes observed ones.
        switch (parsed)
        {
            case EntityType.Player when await players.FindByIdAsync(entityId, cancellationToken) is null:
                throw new EntityNotFoundException("player", entityId);
            case EntityType.Clan when await clans.FindByIdAsync(entityId, cancellationToken) is null:
                throw new EntityNotFoundException("clan", entityId);
        }

        await investigations.AddTargetAsync(investigationId, parsed, entityId, actor, cancellationToken);
        await audit.WriteAsync(AuditActions.InvestigationTargetAdded, $"investigation:{investigation.CaseNumber}",
            new { entityType = parsed.ToString(), entityId }, cancellationToken);

        return await GetWorkspaceAsync(investigationId, cancellationToken);
    }

    public async Task<InvestigationWorkspaceDto> RemoveTargetAsync(Guid investigationId, Guid targetId, CancellationToken cancellationToken = default)
    {
        var investigation = await investigations.FindByIdAsync(investigationId, cancellationToken)
            ?? throw new EntityNotFoundException("investigation", investigationId);

        if (investigation.Status == InvestigationStatus.Archived)
        {
            throw new InvalidOperationException("Archived investigations cannot be modified.");
        }

        if (!await investigations.RemoveTargetAsync(investigationId, targetId, cancellationToken))
        {
            throw new EntityNotFoundException("target", targetId);
        }

        await audit.WriteAsync(AuditActions.InvestigationTargetRemoved, $"investigation:{investigation.CaseNumber}",
            new { targetId }, cancellationToken);

        return await GetWorkspaceAsync(investigationId, cancellationToken);
    }

    public async Task<InvestigationWorkspaceDto> AddNoteAsync(Guid investigationId, string content, string actor, CancellationToken cancellationToken = default)
    {
        var trimmed = content.Trim();
        if (trimmed.Length is < 1 or > 4000)
        {
            throw new ArgumentException("Note content must be between 1 and 4000 characters.");
        }

        var investigation = await investigations.FindByIdAsync(investigationId, cancellationToken)
            ?? throw new EntityNotFoundException("investigation", investigationId);

        if (investigation.Status == InvestigationStatus.Archived)
        {
            throw new InvalidOperationException("Archived investigations cannot be modified.");
        }

        await investigations.AddNoteAsync(investigationId, trimmed, actor, cancellationToken);
        await audit.WriteAsync(AuditActions.InvestigationNoteAdded, $"investigation:{investigation.CaseNumber}", cancellationToken: cancellationToken);

        return await GetWorkspaceAsync(investigationId, cancellationToken);
    }

    public async Task<InvestigationWorkspaceDto> SetStatusAsync(Guid investigationId, bool archived, CancellationToken cancellationToken = default)
    {
        var investigation = await investigations.FindByIdAsync(investigationId, cancellationToken)
            ?? throw new EntityNotFoundException("investigation", investigationId);

        await investigations.SetStatusAsync(investigationId, archived ? InvestigationStatus.Archived : InvestigationStatus.Active, cancellationToken);
        await audit.WriteAsync(
            archived ? AuditActions.InvestigationArchived : AuditActions.InvestigationReopened,
            $"investigation:{investigation.CaseNumber}", cancellationToken: cancellationToken);
        logger.LogInformation("Investigation #{CaseNumber} {Status}", investigation.CaseNumber, archived ? "archived" : "reopened");

        return await GetWorkspaceAsync(investigationId, cancellationToken);
    }

    private async Task<(string DisplayName, int SnapshotCount)> PlayerDetailsAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var player = await players.FindByIdAsync(playerId, cancellationToken);
        if (player is null)
        {
            return ("player (no longer tracked)", 0);
        }

        var snapshots = await players.CountSnapshotsForPlayerAsync(playerId, cancellationToken);
        return (player.Username, snapshots);
    }

    private async Task<(string DisplayName, int SnapshotCount)> ClanDetailsAsync(Guid clanId, CancellationToken cancellationToken)
    {
        var clan = await clans.FindByIdAsync(clanId, cancellationToken);
        return clan is null ? ("clan (no longer tracked)", 0) : (clan.Name ?? $"clan {clan.WolvesvilleClanId[..8]}…", 0);
    }

    private static EntityType ParseEntityType(string entityType) => entityType.Trim().ToLowerInvariant() switch
    {
        "player" => EntityType.Player,
        "clan" => EntityType.Clan,
        _ => throw new ArgumentException($"entityType must be 'player' or 'clan', got '{entityType}'.")
    };

    private static InvestigationSummaryDto ToSummary(Domain.Entities.Investigation investigation, int targetCount, int noteCount) => new(
        investigation.Id,
        investigation.CaseNumber,
        investigation.Title,
        investigation.Description,
        investigation.Status.ToString().ToLowerInvariant(),
        investigation.AssignedToUserId,
        investigation.Tags,
        investigation.CreatedAt,
        investigation.UpdatedAt,
        targetCount,
        noteCount);

    private static Models.TimelineEventDto ToTimelineDto(TimelineEvent timelineEvent) => new(
        timelineEvent.Id,
        timelineEvent.EntityType.ToString().ToLowerInvariant(),
        timelineEvent.EntityId,
        timelineEvent.EventType,
        timelineEvent.Summary,
        timelineEvent.OccurredAt,
        timelineEvent.IsDerived,
        timelineEvent.Confidence);
}
