using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Api.Security;
using Stalksville.Application.Abstractions;

namespace Stalksville.Api.Controllers;

/// <summary>Audit trail viewer: paged, filterable listing of sensitive-operation records.</summary>
[ApiController]
[Route("api/v1/admin/audit")]
[Authorize(Policy = Policies.Admin)]
public sealed class AuditController(IAuditLog audit) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuditEntryView>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? action,
        [FromQuery] string? target,
        [FromQuery] Guid? userId,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var (total, entries) = await audit.QueryAsync(action, target, userId, limit, offset, cancellationToken);
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(entries);
    }
}
