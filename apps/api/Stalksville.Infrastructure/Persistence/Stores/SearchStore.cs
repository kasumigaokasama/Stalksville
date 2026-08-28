using Microsoft.EntityFrameworkCore;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Persistence.Stores;

/// <summary>
/// Workspace search over tracked entities: identifier substring match for players and clans,
/// PostgreSQL full-text search (to_tsvector 'simple') for investigation titles, descriptions and
/// notes. Raw SQL because the UNION across four shapes has no natural LINQ shape.
/// </summary>
public sealed class SearchStore(StalksvilleDbContext db) : ISearchStore
{
    public async Task<IReadOnlyList<SearchHitDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        if (q.Length < 2)
        {
            return [];
        }

        // LIKE metacharacters in user input must not widen the identifier match.
        var like = $"%{q.ToLowerInvariant().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")}%";

        return await db.Database.SqlQuery<SearchHitDto>($"""
            SELECT * FROM (
                SELECT 'player' AS "Type", p."Id"::text AS "Id", p."Username" AS "Title",
                       'tracked player' AS "Subtitle"
                FROM players AS p
                WHERE p."UsernameLower" LIKE {like}
                LIMIT 8
            ) AS players
            UNION ALL
            SELECT * FROM (
                SELECT 'clan' AS "Type", c."Id"::text AS "Id",
                       COALESCE(c."Name", 'clan ' || c."WolvesvilleClanId") AS "Title",
                       'tracked clan' AS "Subtitle"
                FROM clans AS c
                WHERE LOWER(COALESCE(c."Name", '')) LIKE {like} OR c."WolvesvilleClanId" LIKE {like}
                LIMIT 8
            ) AS clans
            UNION ALL
            SELECT * FROM (
                SELECT 'investigation' AS "Type", i."Id"::text AS "Id",
                       '#' || i."CaseNumber" || ' ' || i."Title" AS "Title",
                       i."Description" AS "Subtitle"
                FROM investigations AS i
                WHERE i."Status" = 'Active'
                  AND to_tsvector('simple', i."Title" || ' ' || COALESCE(i."Description", '')) @@ plainto_tsquery('simple', {q})
                LIMIT 8
            ) AS investigations
            UNION ALL
            SELECT * FROM (
                SELECT 'investigation' AS "Type", i."Id"::text AS "Id",
                       '#' || i."CaseNumber" || ' ' || i."Title" AS "Title",
                       'note: ' || LEFT(n."Content", 120) AS "Subtitle"
                FROM investigation_notes AS n
                JOIN investigations AS i ON i."Id" = n."InvestigationId"
                WHERE i."Status" = 'Active'
                  AND to_tsvector('simple', n."Content") @@ plainto_tsquery('simple', {q})
                LIMIT 8
            ) AS notes
            """)
            .ToListAsync(cancellationToken);
    }
}
