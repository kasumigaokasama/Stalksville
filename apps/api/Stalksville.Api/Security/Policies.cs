namespace Stalksville.Api.Security;

/// <summary>Role-based capabilities (master plan §32). All endpoints require authentication by default.</summary>
public static class Policies
{
    /// <summary>Mutating intelligence work: imports, refreshes, case editing (ANALYST, ADMIN).</summary>
    public const string Analyst = "analyst";

    /// <summary>User management (ADMIN only).</summary>
    public const string Admin = "admin";
}
