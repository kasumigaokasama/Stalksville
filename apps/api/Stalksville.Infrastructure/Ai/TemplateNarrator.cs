using Stalksville.Application.Abstractions;
using Stalksville.Application.Ai;
using Stalksville.Application.Investigations;

namespace Stalksville.Infrastructure.Ai;

/// <summary>
/// The default narrator: deterministic, zero-dependency, built entirely from the case workspace.
/// It is also the fallback for LLM narrators — when AI is configured but fails, the product still
/// explains itself.
/// </summary>
public sealed class TemplateNarrator : IAiNarrator
{
    public string ProviderName => "deterministic";

    public string? Model => null;

    public Task<AiNarrative> ExplainInvestigationAsync(InvestigationWorkspaceDto workspace, CancellationToken cancellationToken = default)
    {
        var (observed, derived) = InvestigationFacts.Sections(workspace);

        return Task.FromResult(new AiNarrative(
            ProviderName,
            Model,
            DateTimeOffset.UtcNow,
            observed,
            derived,
            InvestigationFacts.Hypotheses(workspace),
            InvestigationFacts.Unknowns(workspace),
            InvestigationFacts.Disclaimer));
    }
}
