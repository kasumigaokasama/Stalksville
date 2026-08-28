using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Api.Security;
using Stalksville.Application.Advanced;
using Stalksville.Application.Models;
using Stalksville.Application.Players;

namespace Stalksville.Api.Controllers;

[ApiController]
[Route("api/v1/players")]
public sealed class PlayersController(PlayerService players, PlayerIntelligenceService intelligence) : ControllerBase
{
    private string Actor => User.FindFirst("name")?.Value ?? "unknown";

    /// <summary>Local (already tracked) players matching a username fragment.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PlayerSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var result = string.IsNullOrWhiteSpace(query)
            ? await ToRecentAsync(players, cancellationToken)
            : await players.ListLocalAsync(query.Trim(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Exact-username live lookup against Wolvesville; imports/refreshes the player.</summary>
    [HttpGet("lookup")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<PlayerLookupResultDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Lookup([FromQuery] string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new ProblemDetails { Title = "username query parameter is required" });
        }

        var result = await players.LookupAsync(username.Trim(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("compare")]
    [ProducesResponseType<CompareResultDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Compare([FromQuery] Guid a, [FromQuery] Guid b, CancellationToken cancellationToken)
    {
        return Ok(await intelligence.CompareAsync(a, b, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlayerDossierDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDossier(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await players.GetDossierAsync(id, cancellationToken));
    }

    /// <summary>Explainable public-information exposure score — every point lists its evidence.</summary>
    [HttpGet("{id:guid}/exposure")]
    [ProducesResponseType<ExposureResultDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExposure(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await intelligence.GetExposureAsync(id, cancellationToken));
    }

    /// <summary>Anomaly insights derived from the change history, each with evidence change ids.</summary>
    [HttpGet("{id:guid}/insights")]
    [ProducesResponseType<IReadOnlyList<InsightDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInsights(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await intelligence.GetInsightsAsync(id, cancellationToken));
    }

    /// <summary>Observed progression series (level, wins, games) from snapshot history.</summary>
    [HttpGet("{id:guid}/progression")]
    [ProducesResponseType<ProgressionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProgression(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await players.GetProgressionAsync(id, cancellationToken));
    }

    /// <summary>Re-fetches the player from Wolvesville (bypasses cache) and runs the full pipeline.</summary>
    [HttpPost("{id:guid}/refresh")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<PlayerLookupResultDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(Guid id, CancellationToken cancellationToken)
    {
        var result = await players.RefreshAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Erases the player and all derived data from OUR database (data-protection action;
    /// Wolvesville is never contacted). Admin-only, audit-logged with the required reason.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Erase(Guid id, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(new ProblemDetails { Title = "A reason is required for erasure (audit trail)." });
        }

        await players.DeleteAsync(id, reason.Trim(), Actor, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/snapshots")]
    [ProducesResponseType<IReadOnlyList<SnapshotDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSnapshots(
        Guid id,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var (total, snapshots) = await players.GetSnapshotsAsync(id, limit, offset, cancellationToken);
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(snapshots);
    }

    [HttpGet("{id:guid}/changes")]
    [ProducesResponseType<IReadOnlyList<ChangeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChanges(
        Guid id,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var (total, changes) = await players.GetChangesAsync(id, limit, offset, cancellationToken);
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(changes);
    }

    private static async Task<IReadOnlyList<PlayerSummaryDto>> ToRecentAsync(PlayerService service, CancellationToken ct)
    {
        // Empty query: fall back to the recently seen list via a search that matches everything.
        var result = await service.ListLocalAsync(string.Empty, ct);
        return result;
    }
}
