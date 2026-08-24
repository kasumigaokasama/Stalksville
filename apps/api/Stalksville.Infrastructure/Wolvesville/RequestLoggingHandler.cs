using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Persistence;

namespace Stalksville.Infrastructure.Wolvesville;

/// <summary>
/// Writes every Wolvesville API request (final outcome, after retries) to the api_requests
/// provenance table. Logging failures never break the actual request.
/// </summary>
public sealed class RequestLoggingHandler(
    IDbContextFactory<StalksvilleDbContext> dbFactory,
    ILogger<RequestLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        string? error = null;

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            await LogAsync(request, response.StatusCode, (int)response.StatusCode, stopwatch.Elapsed.TotalMilliseconds, started, null, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            await LogAsync(request, null, null, stopwatch.Elapsed.TotalMilliseconds, started, error, cancellationToken);
            throw;
        }
    }

    private async Task LogAsync(
        HttpRequestMessage request, HttpStatusCode? statusCode, int? statusCodeInt, double latencyMs,
        DateTimeOffset requestedAt, string? error, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = request.RequestUri is { } uri
                ? $"{request.Method.Method} {uri.AbsolutePath}{uri.Query}"
                : request.Method.Method;

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.ApiRequests.Add(new ApiRequestLog
            {
                Id = Guid.NewGuid(),
                Method = request.Method.Method,
                Endpoint = endpoint.Length > 256 ? endpoint[..256] : endpoint,
                StatusCode = statusCodeInt,
                LatencyMs = Math.Round(latencyMs, 2),
                RequestedAt = requestedAt,
                ErrorMessage = error is not null && error.Length > 1024 ? error[..1024] : error
            });
            await db.SaveChangesAsync(CancellationToken.None); // never honor the request ct here; the log must survive cancellations
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist Wolvesville API request log for {Endpoint}", request.RequestUri);
        }
    }
}
