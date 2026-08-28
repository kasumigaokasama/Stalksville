namespace Stalksville.Domain.Entities;

/// <summary>
/// A client API key (X-Api-Key) for programmatic access. Only the SHA-256 hash is stored; the
/// full key is shown exactly once at creation. Revoking is soft (RevokedAt) for audit trail.
/// </summary>
public sealed class ApiKey
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Display name, e.g. "nightly-exporter".</summary>
    public required string Name { get; set; }

    /// <summary>First characters of the key, shown in listings so admins can tell keys apart.</summary>
    public required string Prefix { get; set; }

    /// <summary>SHA-256 hex of the full key — never the key itself.</summary>
    public required string KeyHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
