using Microsoft.AspNetCore.Mvc;
using Stalksville.Application.Advanced;

namespace Stalksville.Api.Controllers;

/// <summary>Relationship graph: nodes are tracked entities, edges are derived relationships.</summary>
[ApiController]
[Route("api/v1/graph")]
public sealed class GraphController(GraphService graph) : ControllerBase
{
    /// <summary>Whole graph, or the ego network (1 hop) of an investigation's targets.</summary>
    [HttpGet]
    [ProducesResponseType<GraphDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] Guid? investigationId, CancellationToken cancellationToken)
    {
        return Ok(await graph.BuildAsync(investigationId, cancellationToken));
    }

    /// <summary>Shortest connection paths between two players through shared clans.</summary>
    [HttpGet("paths")]
    [ProducesResponseType<GraphPathsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Paths([FromQuery] Guid from, [FromQuery] Guid to, CancellationToken cancellationToken)
    {
        return Ok(await graph.FindPathsAsync(from, to, cancellationToken));
    }
}
