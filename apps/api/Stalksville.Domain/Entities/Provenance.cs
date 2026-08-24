namespace Stalksville.Domain.Entities;

/// <summary>Provenance log of every request Stalksville makes against the Wolvesville API.</summary>
public sealed class ApiRequestLog
{
    public Guid Id { get; set; }

    public required string Method { get; set; }

    /// <summary>Endpoint path with route placeholders resolved, e.g. "GET /players/123456".</summary>
    public required string Endpoint { get; set; }

    public int? StatusCode { get; set; }

    public double LatencyMs { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public string? ErrorMessage { get; set; }
}

public static class AuditActions
{
    public const string UserLogin = "USER_LOGIN";
    public const string UserLoginFailed = "USER_LOGIN_FAILED";
    public const string PlayerImported = "PLAYER_IMPORTED";
    public const string PlayerRefreshed = "PLAYER_REFRESHED";
    public const string ClanImported = "CLAN_IMPORTED";
    public const string ClanSearched = "CLAN_SEARCHED";
    public const string InvestigationCreated = "INVESTIGATION_CREATED";
    public const string InvestigationArchived = "INVESTIGATION_ARCHIVED";
    public const string InvestigationReopened = "INVESTIGATION_REOPENED";
    public const string InvestigationTargetAdded = "INVESTIGATION_TARGET_ADDED";
    public const string InvestigationTargetRemoved = "INVESTIGATION_TARGET_REMOVED";
    public const string InvestigationNoteAdded = "INVESTIGATION_NOTE_ADDED";
}

/// <summary>Audit trail of sensitive operations performed by Stalksville users.</summary>
public sealed class AuditLog
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public required string Action { get; set; }

    public string? Target { get; set; }

    /// <summary>JSON (jsonb) with operation details.</summary>
    public string? Details { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
