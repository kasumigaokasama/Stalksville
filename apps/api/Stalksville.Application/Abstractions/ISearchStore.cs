using Stalksville.Application.Models;

namespace Stalksville.Application.Abstractions;

/// <summary>Persistence port for workspace search (identifier match + PostgreSQL FTS).</summary>
public interface ISearchStore
{
    /// <summary>Searches tracked players/clans by identifier and active investigations by prose.</summary>
    Task<IReadOnlyList<SearchHitDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
