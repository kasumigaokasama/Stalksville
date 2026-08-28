using Microsoft.AspNetCore.Mvc;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;

namespace Stalksville.Api.Controllers;

/// <summary>
/// The calling user's starred players. Watching is personal bookkeeping: every role may
/// star/unstar, and stars only influence Stalksville's own refresh priorities.
/// </summary>
[ApiController]
[Route("api/v1/watchlist")]
public sealed class WatchlistController(IWatchStore watch) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var id)
        ? id
        : throw new UnauthorizedAccessException("No user claim on the token.");

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WatchedPlayerDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var entries = await watch.ListForUserAsync(UserId, cancellationToken);
        return Ok(entries.Select(e => new WatchedPlayerDto(
            e.Player.Id,
            e.Player.WolvesvillePlayerId,
            e.Player.Username,
            e.Entry.CreatedAt)).ToList());
    }
}
