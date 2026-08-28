using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Api.Security;
using Stalksville.Application.Advanced;
using Stalksville.Application.Models;

namespace Stalksville.Api.Controllers;

/// <summary>Wolvesville ranked leaderboard: the observed board, season context and derived rank shifts.</summary>
[ApiController]
[Route("api/v1/ranked")]
public sealed class RankedController(RankedService ranked) : ControllerBase
{
    /// <summary>The latest captured ranked board (rank = upstream array order; skill points).</summary>
    [HttpGet]
    [ProducesResponseType<RankedBoardDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Board(CancellationToken cancellationToken)
    {
        return Ok(await ranked.GetBoardAsync(cancellationToken));
    }

    /// <summary>Current ranked season window and starting-skill context.</summary>
    [HttpGet("season")]
    [ProducesResponseType<RankedSeasonDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Season(CancellationToken cancellationToken)
    {
        return Ok(await ranked.GetSeasonAsync(cancellationToken));
    }

    /// <summary>Captures the board now and derives rank-shift alerts for tracked players.</summary>
    [HttpPost("capture")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<RankedCaptureResultDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Capture(CancellationToken cancellationToken)
    {
        return Ok(await ranked.CaptureAsync(bypassCache: true, cancellationToken));
    }
}
