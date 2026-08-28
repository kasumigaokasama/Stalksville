using Microsoft.AspNetCore.Mvc;
using Stalksville.Application.Advanced;
using Stalksville.Application.Models;
using Stalksville.Domain.Entities;

namespace Stalksville.Api.Controllers;

/// <summary>
/// Wolvesville cosmetics catalogs (observed reference data): short profile-icon and badge ids
/// resolved to display names, so dossiers and change feeds can show names instead of raw codes.
/// </summary>
[ApiController]
[Route("api/v1/catalog")]
public sealed class CatalogController(CatalogService catalog) : ControllerBase
{
    /// <summary>All catalog items, optionally filtered by kind (profileIcon | badge).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CatalogItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? kind, CancellationToken cancellationToken)
    {
        string? normalized = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            try
            {
                normalized = CatalogKinds.Normalize(kind);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails { Title = ex.Message });
            }
        }

        return Ok(await catalog.GetItemsAsync(normalized, cancellationToken));
    }
}
