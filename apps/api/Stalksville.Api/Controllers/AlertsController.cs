using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;

namespace Stalksville.Api.Controllers;

/// <summary>In-app alert inbox over derived intelligence alerts (evidence-backed by change records).</summary>
[ApiController]
[Route("api/v1/alerts")]
public sealed class AlertsController(IAlertStore alerts) : ControllerBase
{
    private static readonly JsonSerializerOptions EvidenceJson = new(JsonSerializerDefaults.Web);

    /// <summary>Resolves the calling user from the JWT sub claim — read state is per user.</summary>
    private Guid UserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var id)
        ? id
        : throw new UnauthorizedAccessException("No user claim on the token.");

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AlertDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool? unreadOnly,
        [FromQuery] string? kind,
        [FromQuery] Guid? entityId,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var filter = new AlertFilter(
            UnreadOnly: unreadOnly,
            Kind: string.IsNullOrWhiteSpace(kind) ? null : kind,
            EntityId: entityId,
            Limit: limit,
            Offset: offset);

        var list = await alerts.ListAsync(filter, UserId, cancellationToken);
        Response.Headers["X-Total-Count"] = (await alerts.CountAsync(filter, UserId, cancellationToken)).ToString();
        return Ok(list.Select(a => ToDto(a.Alert, a.ReadAt)).ToList());
    }

    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken = default)
    {
        var unread = await alerts.CountUnreadAsync(UserId, cancellationToken);
        return Ok(new UnreadCountDto(unread));
    }

    [HttpPost("{alertId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid alertId, CancellationToken cancellationToken = default)
    {
        var marked = await alerts.MarkReadAsync(alertId, UserId, DateTimeOffset.UtcNow, cancellationToken);
        return marked ? NoContent() : NotFound();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken = default)
    {
        await alerts.MarkAllReadAsync(UserId, DateTimeOffset.UtcNow, cancellationToken);
        return NoContent();
    }

    private static AlertDto ToDto(Alert alert, DateTimeOffset? readAt) => new(
        alert.Id,
        alert.Kind,
        alert.Severity.ToString().ToLowerInvariant(),
        alert.EntityType.ToString().ToLowerInvariant(),
        alert.EntityId,
        alert.EntityTitle,
        alert.Title,
        alert.Body,
        ParseEvidence(alert.Evidence),
        alert.CreatedAt,
        readAt);

    private static AlertEvidenceDto? ParseEvidence(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AlertEvidenceDto>(json, EvidenceJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
