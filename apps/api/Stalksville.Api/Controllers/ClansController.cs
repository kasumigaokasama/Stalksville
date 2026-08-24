using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Api.Security;
using Stalksville.Application.Clans;
using Stalksville.Application.Models;

namespace Stalksville.Api.Controllers;

[ApiController]
[Route("api/v1/clans")]
public sealed class ClansController(ClanService clans) : ControllerBase
{
    /// <summary>Live clan search against Wolvesville (results are transient until imported).</summary>
    [HttpGet("search")]
    [ProducesResponseType<IReadOnlyList<ClanSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string name, [FromQuery] bool exact = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new ProblemDetails { Title = "name query parameter is required" });
        }

        return Ok(await clans.SearchAsync(name.Trim(), exact, cancellationToken));
    }

    /// <summary>Locally tracked clans matching a name fragment.</summary>
    [HttpGet("local")]
    [ProducesResponseType<IReadOnlyList<ClanSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListLocal([FromQuery] string? query, CancellationToken cancellationToken = default)
    {
        var result = string.IsNullOrWhiteSpace(query)
            ? await clans.ListLocalAsync(string.Empty, cancellationToken)
            : await clans.ListLocalAsync(query.Trim(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClanDossierDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDossier(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await clans.GetDossierAsync(id, cancellationToken));
    }

    /// <summary>Imports (or re-imports) a Wolvesville clan: info + all members through the player pipeline.</summary>
    [HttpPost("{wolvesvilleClanId}/import")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<ClanDossierDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(string wolvesvilleClanId, CancellationToken cancellationToken)
    {
        var result = await clans.ImportAsync(wolvesvilleClanId, cancellationToken);
        return Ok(result);
    }
}
