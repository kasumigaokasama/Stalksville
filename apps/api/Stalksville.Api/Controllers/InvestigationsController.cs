using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stalksville.Api.Security;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Advanced;
using Stalksville.Application.Ai;
using Stalksville.Application.Investigations;
using Stalksville.Domain.Entities;

namespace Stalksville.Api.Controllers;

public sealed record AssigneeDto(Guid Id, string Username);

[ApiController]
[Route("api/v1/investigations")]
public sealed class InvestigationsController(
    InvestigationService investigations,
    IUserStore users,
    IAiNarrator narrator,
    InvestigationExporter exporter) : ControllerBase
{
    private string Actor => User.FindFirst("name")?.Value ?? "unknown";

    /// <summary>Users available for case assignment (analysts and admins).</summary>
    [HttpGet("assignees")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<IReadOnlyList<AssigneeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Assignees(CancellationToken cancellationToken)
    {
        var list = await users.ListAsync(cancellationToken);
        return Ok(list
            .Where(u => u.Role is UserRole.Analyst or UserRole.Admin)
            .Select(u => new AssigneeDto(u.Id, u.Username))
            .ToList());
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InvestigationSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeArchived = false,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var (total, items) = await investigations.ListAsync(includeArchived, limit, offset, cancellationToken);
        Response.Headers["X-Total-Count"] = total.ToString();
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<InvestigationWorkspaceDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateInvestigationRequest request, CancellationToken cancellationToken)
    {
        var workspace = await investigations.CreateAsync(request.Title, request.Description, Actor, cancellationToken);
        return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.Investigation.Id }, workspace);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<InvestigationWorkspaceDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkspace(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await investigations.GetWorkspaceAsync(id, cancellationToken));
    }

    /// <summary>Partial update: title, description, assignee and tags. Omitted fields stay put.</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<InvestigationWorkspaceDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInvestigationRequest request, CancellationToken cancellationToken)
    {
        var workspace = await investigations.UpdateAsync(
            id,
            request.Title,
            request.Description,
            request.AssignedToUserId,
            request.AssigneeProvided,
            request.Tags,
            cancellationToken);
        return Ok(workspace);
    }

    [HttpPost("{id:guid}/targets")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<InvestigationWorkspaceDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddTarget(Guid id, [FromBody] AddTargetRequest request, CancellationToken cancellationToken)
    {
        return Ok(await investigations.AddTargetAsync(id, request.EntityType, request.EntityId, Actor, cancellationToken));
    }

    [HttpDelete("{id:guid}/targets/{targetId:guid}")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<InvestigationWorkspaceDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveTarget(Guid id, Guid targetId, CancellationToken cancellationToken)
    {
        return Ok(await investigations.RemoveTargetAsync(id, targetId, cancellationToken));
    }

    [HttpPost("{id:guid}/notes")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<InvestigationWorkspaceDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] AddNoteRequest request, CancellationToken cancellationToken)
    {
        return Ok(await investigations.AddNoteAsync(id, request.Content, Actor, cancellationToken));
    }

    /// <summary>Explains the case with the Observed/Derived/Hypothesis/Unknown guardrail (plan §40/§41).</summary>
    [HttpPost("{id:guid}/explain")]
    [ProducesResponseType<AiNarrative>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Explain(Guid id, CancellationToken cancellationToken)
    {
        var workspace = await investigations.GetWorkspaceAsync(id, cancellationToken);
        var narrative = await narrator.ExplainInvestigationAsync(workspace, cancellationToken);
        return Ok(narrative);
    }

    /// <summary>Case report export: Markdown, CSV (timeline) or JSON (plan §35).</summary>
    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromQuery] string format = "md", CancellationToken cancellationToken = default)
    {
        var (contentType, fileName, content) = await exporter.ExportAsync(id, format, cancellationToken);
        return File(content, contentType, fileName);
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<InvestigationWorkspaceDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await investigations.SetStatusAsync(id, archived: true, cancellationToken));
    }

    [HttpPost("{id:guid}/reopen")]
    [Authorize(Policy = Policies.Analyst)]
    [ProducesResponseType<InvestigationWorkspaceDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reopen(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await investigations.SetStatusAsync(id, archived: false, cancellationToken));
    }
}
