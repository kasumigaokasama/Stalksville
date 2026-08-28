using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Api.Security;
using Stalksville.Application.Advanced;
using Stalksville.Application.Models;

namespace Stalksville.Api.Controllers;

/// <summary>Wolvesville highscore boards: observed top-100 XP lists and derived rank shifts.</summary>
[ApiController]
[Route("api/v1/highscores")]
public sealed class HighscoresController(HighscoreService highscores) : ControllerBase
{
    /// <summary>The latest captured board of a period (alltime/monthly/weekly/daily).</summary>
    [HttpGet]
    [ProducesResponseType<HighscoreBoardDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Board([FromQuery] string period = "alltime", CancellationToken cancellationToken = default)
    {
        return Ok(await highscores.GetBoardAsync(period, cancellationToken));
    }

    /// <summary>Captures all four boards now and derives rank-shift alerts for tracked players.</summary>
    [HttpPost("capture")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<HighscoreCaptureResultDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Capture(CancellationToken cancellationToken)
    {
        return Ok(await highscores.CaptureAsync(bypassCache: true, cancellationToken));
    }
}
