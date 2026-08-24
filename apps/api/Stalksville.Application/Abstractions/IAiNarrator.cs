using Stalksville.Application.Ai;
using Stalksville.Application.Investigations;

namespace Stalksville.Application.Abstractions;

/// <summary>
/// AI is optional by design (master plan §40): narrators explain what the deterministic engine
/// already computed. Implementations must never invent facts — they work from the case workspace.
/// </summary>
public interface IAiNarrator
{
    string ProviderName { get; }

    string? Model { get; }

    Task<AiNarrative> ExplainInvestigationAsync(InvestigationWorkspaceDto workspace, CancellationToken cancellationToken = default);
}
