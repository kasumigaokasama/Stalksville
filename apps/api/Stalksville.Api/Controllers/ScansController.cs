using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Api.Security;
using Stalksville.Application.Advanced;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;

namespace Stalksville.Api.Controllers;

/// <summary>
/// Scheduled scans and change-notification channels (ADMIN). Schedules execute through the same
/// application services as the manual UI actions; webhook URLs are credentials and are masked in
/// responses.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.Admin)]
[Route("api/v1/scans")]
public sealed class ScansController(ScanService scans) : ControllerBase
{
    // ---- schedules ----

    [HttpGet("schedules")]
    [ProducesResponseType<IReadOnlyList<ScanScheduleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSchedules(CancellationToken cancellationToken)
    {
        var schedules = await scans.ListSchedulesAsync(cancellationToken);
        return Ok(schedules.Select(ToDto).ToList());
    }

    [HttpPost("schedules")]
    [ProducesResponseType<ScanScheduleDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSchedule(UpsertScanScheduleDto request, CancellationToken cancellationToken)
    {
        var schedule = await scans.CreateScheduleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListSchedules), new { id = schedule.Id }, ToDto(schedule));
    }

    [HttpPatch("schedules/{id:guid}")]
    [ProducesResponseType<ScanScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSchedule(Guid id, UpsertScanScheduleDto request, CancellationToken cancellationToken)
    {
        var schedule = await scans.UpdateScheduleAsync(id, request, cancellationToken);
        return schedule is null ? NotFound() : Ok(ToDto(schedule));
    }

    [HttpDelete("schedules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken cancellationToken)
        => await scans.DeleteScheduleAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("schedules/{id:guid}/run")]
    [ProducesResponseType<ScanRunDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RunNow(Guid id, CancellationToken cancellationToken)
    {
        var run = await scans.RunNowAsync(id, cancellationToken);
        return run is null ? NotFound() : Ok(ToDto(run));
    }

    // ---- run history ----

    [HttpGet("runs")]
    [ProducesResponseType<IReadOnlyList<ScanRunDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRuns([FromQuery] int limit = 25, [FromQuery] int offset = 0, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);
        var runs = await scans.ListRunsAsync(limit, offset, cancellationToken);
        return Ok(runs.Select(ToDto).ToList());
    }

    // ---- notification channels ----

    [HttpGet("notifications/channels")]
    [ProducesResponseType<IReadOnlyList<NotificationChannelDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListChannels(CancellationToken cancellationToken)
    {
        var channels = await scans.ListChannelsAsync(cancellationToken);
        return Ok(channels.Select(ToDto).ToList());
    }

    [HttpPost("notifications/channels")]
    [ProducesResponseType<NotificationChannelDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateChannel(UpsertNotificationChannelDto request, CancellationToken cancellationToken)
    {
        var channel = await scans.CreateChannelAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListChannels), new { id = channel.Id }, ToDto(channel));
    }

    [HttpPatch("notifications/channels/{id:guid}")]
    [ProducesResponseType<NotificationChannelDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateChannel(Guid id, UpsertNotificationChannelDto request, CancellationToken cancellationToken)
    {
        var channel = await scans.UpdateChannelAsync(id, request, cancellationToken);
        return channel is null ? NotFound() : Ok(ToDto(channel));
    }

    [HttpDelete("notifications/channels/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteChannel(Guid id, CancellationToken cancellationToken)
        => await scans.DeleteChannelAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Sends a fixed test payload so a channel can be verified before relying on it.</summary>
    [HttpPost("notifications/channels/{id:guid}/test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestChannel(Guid id, CancellationToken cancellationToken)
    {
        var result = await scans.TestChannelAsync(id, cancellationToken);
        return result is null
            ? NotFound()
            : Ok(new { success = result.Value.Success, detail = result.Value.Detail });
    }

    private static ScanScheduleDto ToDto(ScanSchedule s) => new(
        s.Id, s.Name, s.Kind, s.IntervalMinutes, s.BatchSize, s.Enabled, s.CreatedAt, s.UpdatedAt, s.LastRunAt, s.NextRunAt);

    private static ScanRunDto ToDto(ScanRun r) => new(
        r.Id, r.ScheduleId, r.ScheduleName, r.Status, r.StartedAt, r.FinishedAt,
        r.PlayersObserved, r.ChangesDetected, r.AlertsRaised, r.Error,
        ScanService.DeserializeLines(r.DetailJson).Select(l => new ScanChangeLineDto(l.Player, l.Changes)).ToList());

    /// <summary>Webhook URLs embed secrets (Discord tokens) — show scheme/host and a path hint only.</summary>
    private static NotificationChannelDto ToDto(NotificationChannel c) => new(
        c.Id, c.Name, c.Kind, MaskUrl(c.TargetUrl), c.Enabled, c.CreatedAt, c.LastDeliveryAt, c.LastDeliveryStatus);

    private static string MaskUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "(invalid URL)";
        }

        var path = uri.AbsolutePath.Trim('/');
        var firstSegment = path.Contains('/') ? path[..path.IndexOf('/')] : path;
        var hint = firstSegment.Length > 8 ? $"{firstSegment[..8]}…" : firstSegment;
        return $"{uri.Scheme}://{uri.Host}/{hint}";
    }
}
