using Microsoft.AspNetCore.Mvc;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;

namespace Stalksville.Api.Controllers;

/// <summary>Global timeline over all recorded events with optional filters (plan §17).</summary>
[ApiController]
[Route("api/v1/timeline")]
public sealed class TimelineController(IDerivationStore derivations) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TimelineEventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? entity,
        [FromQuery] string? eventType,
        [FromQuery] bool? derived,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        EntityType? entityType = entity?.Trim().ToLowerInvariant() switch
        {
            "player" => EntityType.Player,
            "clan" => EntityType.Clan,
            null or "" => null,
            _ => throw new ArgumentException($"entity filter must be 'player' or 'clan', got '{entity}'.")
        };

        var events = await derivations.GetFilteredTimelineAsync(
            new TimelineFilter(entityType, null, string.IsNullOrWhiteSpace(eventType) ? null : eventType, derived, limit),
            cancellationToken);

        return Ok(events.Select(e => new TimelineEventDto(
            e.Id,
            e.EntityType.ToString().ToLowerInvariant(),
            e.EntityId,
            e.EventType,
            e.Summary,
            e.OccurredAt,
            e.IsDerived,
            e.Confidence)).ToList());
    }
}
