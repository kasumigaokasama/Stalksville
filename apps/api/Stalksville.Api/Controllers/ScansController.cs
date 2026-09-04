using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Api.Security;
using Stalksville.Application.Abstractions;
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
public sealed class ScansController(ScanService scans, IPlayerStore players) : ControllerBase
{
    // ---- schedules ----

    [HttpGet("schedules")]
    [ProducesResponseType<IReadOnlyList<ScanScheduleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSchedules(CancellationToken cancellationToken)
    {
        var views = await scans.ListSchedulesWithSelectionsAsync(cancellationToken);
        var names = await ResolveNamesAsync(views.SelectMany(v => v.SelectedPlayerIds).Distinct(), cancellationToken);
        return Ok(views.Select(v => ToDto(v, names)).ToList());
    }

    [HttpPost("schedules")]
    [ProducesResponseType<ScanScheduleDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSchedule(UpsertScanScheduleDto request, CancellationToken cancellationToken)
    {
        var schedule = await scans.CreateScheduleAsync(request, cancellationToken);
        var selection = await scans.ListSchedulesWithSelectionsAsync(cancellationToken);
        var view = selection.FirstOrDefault(v => v.Schedule.Id == schedule.Id) ?? new(schedule, []);
        var names = await ResolveNamesAsync(view.SelectedPlayerIds, cancellationToken);
        return CreatedAtAction(nameof(ListSchedules), new { id = schedule.Id }, ToDto(view, names));
    }

    [HttpPatch("schedules/{id:guid}")]
    [ProducesResponseType<ScanScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSchedule(Guid id, UpsertScanScheduleDto request, CancellationToken cancellationToken)
    {
        var schedule = await scans.UpdateScheduleAsync(id, request, cancellationToken);
        if (schedule is null)
        {
            return NotFound();
        }

        var selectedIds = await scans.GetSelectedPlayerIdsAsync(id, cancellationToken);
        var names = await ResolveNamesAsync(selectedIds, cancellationToken);
        return Ok(ToDto(new ScanScheduleView(schedule, selectedIds), names));
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

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids as IReadOnlyList<Guid> ?? ids.ToList();
        if (list.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var found = await players.GetPlayersByIdsAsync(list, cancellationToken);
        return found.ToDictionary(p => p.Id, p => p.Username);
    }

    private static ScanScheduleDto ToDto(ScanScheduleView view, IReadOnlyDictionary<Guid, string> names) => new(
        view.Schedule.Id,
        view.Schedule.Name,
        view.Schedule.Kind,
        view.Schedule.IntervalMinutes,
        view.Schedule.BatchSize,
        view.Schedule.Enabled,
        view.Schedule.PlayerScope,
        view.SelectedPlayerIds.Select(id => names.TryGetValue(id, out var username) ? username : id.ToString()).ToList(),
        view.Schedule.CreatedAt,
        view.Schedule.UpdatedAt,
        view.Schedule.LastRunAt,
        view.Schedule.NextRunAt);

    private static ScanRunDto ToDto(ScanRun r) => new(
        r.Id, r.ScheduleId, r.ScheduleName, r.Status, r.StartedAt, r.FinishedAt,
        r.PlayersObserved, r.ChangesDetected, r.AlertsRaised, r.Error,
        ScanService.DeserializeLines(r.DetailJson).Select(ToDto).ToList());

    private static ScanChangeLineDto ToDto(ScanChangeLine line) => new(
        line.Player,
        line.Changes,
        (line.Fields ?? []).Select(f => new ScanFieldChangeDto(f.Field, f.OldValue, f.NewValue)).ToList());

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
