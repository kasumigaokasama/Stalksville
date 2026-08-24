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
}
