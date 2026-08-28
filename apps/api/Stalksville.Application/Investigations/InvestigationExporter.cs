using System.Text;
using System.Text.Json;
using Stalksville.Application.Advanced;
using Stalksville.Application.Ai;
using Stalksville.Application.Models;

namespace Stalksville.Application.Investigations;

/// <summary>
/// Case report exports (master plan §35): Markdown (human report), CSV (timeline) and JSON
/// (full workspace + graph). Always includes the confidence & limitations section.
/// </summary>
public sealed class InvestigationExporter(
    InvestigationService investigations,
    GraphService graph)
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public async Task<(string ContentType, string FileName, byte[] Content)> ExportAsync(
        Guid investigationId, string format, CancellationToken cancellationToken = default)
    {
        var workspace = await investigations.GetWorkspaceAsync(investigationId, cancellationToken);
        var padded = workspace.Investigation.CaseNumber.ToString().PadLeft(4, '0');

        return format.Trim().ToLowerInvariant() switch
        {
            "md" or "markdown" => ("text/markdown; charset=utf-8", $"stalksville-case-{padded}.md",
                Encoding.UTF8.GetBytes(ToMarkdown(workspace, await BuildRelationshipLinesAsync(investigationId, cancellationToken)))),
            "csv" => ("text/csv; charset=utf-8", $"stalksville-case-{padded}-timeline.csv",
                Encoding.UTF8.GetBytes(ToCsv(workspace))),
            "json" => ("application/json", $"stalksville-case-{padded}.json",
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(workspace, WebJson))),
            // Print-optimized HTML: open in a browser, Ctrl+P → PDF. Zero-dependency by design.
            "html" => ("text/html; charset=utf-8", $"stalksville-case-{padded}.html",
                Encoding.UTF8.GetBytes(ToHtml(workspace, await BuildRelationshipLinesAsync(investigationId, cancellationToken)))),
            _ => throw new ArgumentException("format must be md, csv, json or html.")
        };
    }

    private static string ToHtml(InvestigationWorkspaceDto workspace, List<string> relationships)
    {
        // The report body is the Markdown report rendered as preformatted text with print CSS;
        // open in a browser, Ctrl+P → PDF. Zero-dependency by design.
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <title>Stalksville case {{workspace.Investigation.CaseNumber:0000}}</title>
              <style>
                body { margin: 2.2cm; background: #fff; }
                pre { white-space: pre-wrap; font: 13px/1.55 Georgia, 'Times New Roman', serif; color: #111; }
                @media print { body { margin: 0; } }
              </style>
            </head>
            <body>
              <pre>{{EncodeHtml(ToMarkdown(workspace, relationships))}}</pre>
              <script>window.addEventListener('load', () => setTimeout(() => window.print(), 300));</script>
            </body>
            </html>
            """;
    }

    private static string EncodeHtml(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    private async Task<List<string>> BuildRelationshipLinesAsync(Guid investigationId, CancellationToken cancellationToken)
    {
        var graphData = await graph.BuildAsync(investigationId, cancellationToken);
        var labelById = graphData.Nodes.ToDictionary(n => n.Id, n => n.Label);

        return graphData.Edges
            .Select(edge => $"{labelById.GetValueOrDefault(edge.Source, "?")} —{edge.Type} ({(edge.Confidence * 100).ToString("F0")}%, {(edge.IsCurrent ? "current" : "historical")})→ {labelById.GetValueOrDefault(edge.Target, "?")}")
            .ToList();
    }

    private static string ToMarkdown(InvestigationWorkspaceDto workspace, List<string> relationships)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Stalksville Investigation Report");
        sb.AppendLine();
        sb.AppendLine($"**Case #{workspace.Investigation.CaseNumber.ToString().PadLeft(4, '0')} — {workspace.Investigation.Title}**");
        sb.AppendLine();
        sb.AppendLine($"- Status: {workspace.Investigation.Status}");
        sb.AppendLine($"- Created: {workspace.Investigation.CreatedAt:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine($"- Report generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
        if (!string.IsNullOrEmpty(workspace.Investigation.Description))
        {
            sb.AppendLine($"- Description: {workspace.Investigation.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("## Targets");
        sb.AppendLine();
        foreach (var target in workspace.Targets)
        {
            sb.AppendLine($"- **{target.DisplayName}** ({target.EntityType}) — {target.SnapshotCount} snapshot(s), {target.CurrentRelationships} current relationship(s), added {target.AddedAt:yyyy-MM-dd} by {target.AddedBy}");
        }

        sb.AppendLine();
        sb.AppendLine("## Timeline");
        sb.AppendLine();
        sb.AppendLine("| Date | Type | Observed/Derived | Event |");
        sb.AppendLine("| --- | --- | --- | --- |");
        foreach (var timelineEvent in workspace.Timeline)
        {
            var kind = timelineEvent.IsDerived ? "derived" : "observed";
            var summary = (timelineEvent.Summary ?? timelineEvent.EventType).Replace("|", "\\|");
            sb.AppendLine($"| {timelineEvent.OccurredAt:yyyy-MM-dd} | {timelineEvent.EventType} | {kind} | {summary} ({timelineEvent.EntityType}) |");
        }

        sb.AppendLine();
        sb.AppendLine("## Relationships (derived)");
        sb.AppendLine();
        if (relationships.Count == 0)
        {
            sb.AppendLine("_No relationships in the case scope._");
        }
        else
        {
            foreach (var line in relationships)
            {
                sb.AppendLine($"- {line}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Analyst notes");
        sb.AppendLine();
        if (workspace.Notes.Count == 0)
        {
            sb.AppendLine("_None._");
        }
        else
        {
            foreach (var note in workspace.Notes)
            {
                sb.AppendLine($"- **{note.Author}** ({note.CreatedAt:yyyy-MM-dd}): {note.Content}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Confidence & limitations");
        sb.AppendLine();
        sb.AppendLine(InvestigationFacts.Disclaimer);

        return sb.ToString();
    }

    private static string ToCsv(InvestigationWorkspaceDto workspace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("occurred_at,event_type,entity_type,entity_id,observed_or_derived,summary");
        foreach (var timelineEvent in workspace.Timeline)
        {
            var summary = (timelineEvent.Summary ?? timelineEvent.EventType).Replace("\"", "\"\"");
            sb.AppendLine($"\"{timelineEvent.OccurredAt:yyyy-MM-dd HH:mm:ss}\",\"{timelineEvent.EventType}\",\"{timelineEvent.EntityType}\",\"{timelineEvent.EntityId}\",\"{(timelineEvent.IsDerived ? "derived" : "observed")}\",\"{summary}\"");
        }

        return sb.ToString();
    }
}
