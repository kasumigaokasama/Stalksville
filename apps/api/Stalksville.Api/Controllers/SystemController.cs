using Microsoft.AspNetCore.Mvc;
using Stalksville.Application.Models;
using Stalksville.Application.System;

namespace Stalksville.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController(SystemService system) : ControllerBase
{
    /// <summary>Wolvesville connectivity + capability report (read ✓ / requires clan bot / write disabled).</summary>
    [HttpGet("wolvesville/status")]
    [ProducesResponseType<ConnectionStatusDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> WolvesvilleStatus(CancellationToken cancellationToken)
    {
        return Ok(await system.GetConnectionStatusAsync(cancellationToken));
    }

    [HttpGet("dashboard")]
    [ProducesResponseType<DashboardStatsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        return Ok(await system.GetDashboardAsync(cancellationToken));
    }

    [HttpGet("timeline")]
    [ProducesResponseType<IReadOnlyList<TimelineEventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Timeline([FromQuery] int limit = 30, CancellationToken cancellationToken = default)
    {
        return Ok(await system.GetRecentTimelineAsync(Math.Clamp(limit, 1, 200), cancellationToken));
    }
}
