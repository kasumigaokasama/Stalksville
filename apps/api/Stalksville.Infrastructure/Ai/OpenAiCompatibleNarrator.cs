using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Ai;
using Stalksville.Application.Investigations;

namespace Stalksville.Infrastructure.Ai;

/// <summary>
/// LLM narrator against any OpenAI-compatible chat-completions API. The prompt architecture
/// forces FACTS + EVIDENCE + SOURCES + CONFIDENCE into context and demands the four-section
/// guardrail structure in JSON. Any failure degrades to the deterministic narrator — AI is
/// optional, the explanation is not.
/// </summary>
public sealed class OpenAiCompatibleNarrator(
    HttpClient http,
    IOptions<AiOptions> options,
    TemplateNarrator fallback,
    ILogger<OpenAiCompatibleNarrator> logger) : IAiNarrator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string ProviderName => "openai-compatible";

    public string? Model => options.Value.Model;

    private record ChatMessage(string Role, string Content);

    private record ChatRequest(string Model, IReadOnlyList<ChatMessage> Messages, bool Stream);

    private record ChatResponse(IReadOnlyList<Choice> Choices);

    private record Choice(ChatMessage Message);

    public async Task<AiNarrative> ExplainInvestigationAsync(InvestigationWorkspaceDto workspace, CancellationToken cancellationToken = default)
    {
        try
        {
            var (observed, derived) = InvestigationFacts.Sections(workspace);
            var facts = JsonSerializer.Serialize(new
            {
                @case = new { workspace.Investigation.CaseNumber, workspace.Investigation.Title, status = workspace.Investigation.Status },
                targets = workspace.Targets.Select(t => new { t.DisplayName, t.EntityType, t.SnapshotCount, t.CurrentRelationships }),
                stats = workspace.Stats,
                observedEvents = observed,
                derivedEvents = derived,
                deterministicHypotheses = InvestigationFacts.Hypotheses(workspace),
                deterministicUnknowns = InvestigationFacts.Unknowns(workspace)
            }, Json);

            var request = new ChatRequest(
                options.Value.Model,
                [
                    new ChatMessage("system", SystemPrompt),
                    new ChatMessage("user", $"Explain this Stalksville investigation. Facts (JSON):\n{facts}")
                ],
                Stream: false);

            var response = await http.PostAsJsonAsync("chat/completions", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var chat = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
            var content = chat?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("Empty completion.");
            }

            var parsed = JsonSerializer.Deserialize<Dictionary<string, IReadOnlyList<string>>>(content, Json)
                ?? throw new InvalidOperationException("Unparseable completion.");

            return new AiNarrative(
                ProviderName,
                Model,
                DateTimeOffset.UtcNow,
                Take(parsed, "observed", observed),
                Take(parsed, "derived", derived),
                Take(parsed, "hypothesis", InvestigationFacts.Hypotheses(workspace)),
                Take(parsed, "unknown", InvestigationFacts.Unknowns(workspace)),
                InvestigationFacts.Disclaimer);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM narration failed; falling back to the deterministic narrator");
            return await fallback.ExplainInvestigationAsync(workspace, cancellationToken);
        }
    }

    private const string SystemPrompt =
        "You explain Wolvesville investigations for the Stalksville intelligence workbench. "
        + "You MUST NOT invent facts: use ONLY the supplied facts, evidence and confidence values. "
        + "Respond with a single JSON object with keys 'observed', 'derived', 'hypothesis', 'unknown' — each an array of short strings. "
        + "'observed' = facts returned directly by Wolvesville. 'derived' = conclusions Stalksville computed (cite the evidence). "
        + "'hypothesis' = unproven possibilities, clearly framed as such. 'unknown' = explicit data gaps. "
        + "Never claim two accounts belong to the same real-world person.";

    private static IReadOnlyList<string> Take(Dictionary<string, IReadOnlyList<string>> parsed, string key, IReadOnlyList<string> deterministic)
        => parsed.TryGetValue(key, out var value) && value is { Count: > 0 } ? value : deterministic;
}
