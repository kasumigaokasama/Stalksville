namespace Stalksville.Domain.Entities;

public sealed class Clan
{
    public Guid Id { get; set; }

    public required string WolvesvilleClanId { get; set; }

    /// <summary>Null while the clan is only known from a player profile and has not been imported yet.</summary>
    public string? Name { get; set; }

    public string? Description { get; set; }

    public int? MemberCount { get; set; }

    public string? LanguageCode { get; set; }

    public string? JoinType { get; set; }

    public long? Xps { get; set; }

    public int? Level { get; set; }

    public string? LeaderWolvesvillePlayerId { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? LastImportedAt { get; set; }

    public List<ClanMembership> Memberships { get; set; } = [];
}

/// <summary>
/// Observed-over-time membership of a player in a clan. Current memberships have
/// <see cref="EndedAt"/> = null. Derived from snapshots / clan member imports.
/// </summary>
public sealed class ClanMembership
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public Player? Player { get; set; }

    public Guid ClanId { get; set; }

    public Clan? Clan { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public bool IsCurrent => EndedAt is null;

    /// <summary>Provenance of the first observation, e.g. "wolvesville:GET /players/{playerId}".</summary>
    public required string Source { get; set; }
}
