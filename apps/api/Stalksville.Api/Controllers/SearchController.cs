using Microsoft.AspNetCore.Mvc;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;

namespace Stalksville.Api.Controllers;

/// <summary>Workspace-wide search: identifier match for tracked entities, FTS for case prose.</summary>
[ApiController]
[Route("api/v1/search")]
public sealed class SearchController(ISearchStore search) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SearchHitDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return Ok(Array.Empty<SearchHitDto>());
        }

        return Ok(await search.SearchAsync(q, cancellationToken));
    }
}
