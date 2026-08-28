using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Auditing;

/// <summary>
/// Persists audit entries; resolves the acting user from the current JWT (sub claim) when
/// available. Audit failures are logged but never break the audited operation.
/// </summary>
public sealed class AuditLogger(
    IDbContextFactory<StalksvilleDbContext> dbFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLogger> logger) : IAuditLog
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task WriteAsync(string action, string? target = null, object? details = null, CancellationToken cancellationToken = default)
    {
        try
        {
            Guid? userId = null;
            if (httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value is { } sub
                && Guid.TryParse(sub, out var parsed))
            {
                userId = parsed;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                Target = target is not null && target.Length > 128 ? target[..128] : target,
                Details = details is null ? null : JsonSerializer.Serialize(details, Json),
                OccurredAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist audit entry {Action}", action);
        }
    }

    public async Task<(int Total, IReadOnlyList<AuditEntryView> Entries)> QueryAsync(
        string? action = null,
        string? target = null,
        Guid? userId = null,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<AuditLog> query = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action.StartsWith(action));
        }
        if (!string.IsNullOrWhiteSpace(target))
        {
            query = query.Where(a => a.Target!.StartsWith(target));
        }
        if (userId is { } user)
        {
            query = query.Where(a => a.UserId == user);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(a => a.OccurredAt)
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);

        var userIds = rows.Where(a => a.UserId is not null).Select(a => a.UserId!.Value).Distinct().ToList();
        var usernames = userIds.Count > 0
            ? await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, cancellationToken)
            : new Dictionary<Guid, string>();

        return (total, rows.Select(a => new AuditEntryView(
            a.Id,
            a.UserId,
            a.UserId is { } id && usernames.TryGetValue(id, out var username) ? username : null,
            a.Action,
            a.Target,
            a.Details,
            a.OccurredAt)).ToList());
    }
}
