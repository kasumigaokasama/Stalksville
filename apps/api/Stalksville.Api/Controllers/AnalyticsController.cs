using Microsoft.AspNetCore.Mvc;
using Stalksville.Application.Advanced;

namespace Stalksville.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
public sealed class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<AnalyticsSummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        return Ok(await analytics.GetSummaryAsync(days, cancellationToken));
    }
}
