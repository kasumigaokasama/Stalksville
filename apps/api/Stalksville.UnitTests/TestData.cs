using Stalksville.Domain.Entities;
using Stalksville.Domain.Models;

namespace Stalksville.UnitTests;

public static class TestData
{
    public static NormalizedPlayerState Player(
        string id = "1001",
        string username = "shadowfox",
        int? level = 80,
        string? clanId = "2001",
        string? personalMessage = null,
        string[]? badges = null,
        DateTimeOffset? lastOnline = null,
        int wins = 100,
        int losses = 50) => new()
    {
        WolvesvillePlayerId = id,
        Username = username,
        PersonalMessage = personalMessage,
        Level = level,
        Status = "ONLINE",
        LastOnline = lastOnline,
        ClanWolvesvilleId = clanId,
        Wins = wins,
        Losses = losses,
        GamesPlayed = wins + losses,
        BadgeIds = badges ?? ["badge_hunter"],
        ProfileIconId = "icon_default",
        EquippedAvatarId = "avatar_1001"
    };

    public static ClanMembership Membership(Guid playerId, Guid clanId, DateTimeOffset startedAt, DateTimeOffset? endedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        PlayerId = playerId,
        ClanId = clanId,
        StartedAt = startedAt,
        EndedAt = endedAt,
        Source = "test"
    };
}
