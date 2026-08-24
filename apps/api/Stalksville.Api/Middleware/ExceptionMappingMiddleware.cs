using Stalksville.Application;

namespace Stalksville.Api.Middleware;

/// <summary>Maps application exceptions to ProblemDetails responses.</summary>
public sealed class ExceptionMappingMiddleware(RequestDelegate next, ILogger<ExceptionMappingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (EntityNotFoundException ex)
        {
            await WriteProblem(context, StatusCodes.Status404NotFound, ex.Message, ex.EntityType);
        }
        catch (ArgumentException ex)
        {
            await WriteProblem(context, StatusCodes.Status400BadRequest, ex.Message, "validation");
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Blocked outbound request", StringComparison.Ordinal))
        {
            // HostGuard rejected an outbound request — misconfiguration, not user error.
            logger.LogError(ex, "Outbound request blocked");
            await WriteProblem(context, StatusCodes.Status500InternalServerError, "Upstream configuration rejected by security policy", "configuration");
        }
        catch (WolvesvilleApiException ex)
        {
            var (status, title) = ex.StatusCode switch
            {
                404 => (StatusCodes.Status404NotFound, "Wolvesville has no such entity"),
                429 => (StatusCodes.Status503ServiceUnavailable, "Wolvesville is rate limiting Stalksville; retry shortly"),
                _ => (StatusCodes.Status502BadGateway, "Wolvesville API error")
            };

            logger.LogWarning("Wolvesville upstream error {Status} mapped to {Mapped}: {Message}", ex.StatusCode, status, ex.Message);
            await WriteProblem(context, status, $"{title}: {ex.Message}", "upstream");
        }
        catch (InvalidOperationException ex)
        {
            // Conflicting state (e.g. modifying an archived investigation).
            await WriteProblem(context, StatusCodes.Status409Conflict, ex.Message, "conflict");
        }
    }

    private static async Task WriteProblem(HttpContext context, int status, string detail, string type)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = "Stalksville request failed",
            Detail = detail,
            Type = type
        });
    }

    private sealed class ProblemDetails
    {
        public int Status { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;
    }
}
