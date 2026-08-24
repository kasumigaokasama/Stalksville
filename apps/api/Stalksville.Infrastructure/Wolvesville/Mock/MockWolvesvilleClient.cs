using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Stalksville.Application;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Models;

namespace Stalksville.Infrastructure.Wolvesville.Mock;

/// <summary>
/// In-process demo dataset (Wolvesville:Mode=Mock) so the whole core loop — including change
/// detection — can be exercised without a real API key. The player "shadowfox" is scripted to
/// switch clans on every fetch and level up on every third fetch, so refreshing demonstrates
/// snapshots, changes, membership transitions and relationships.
/// </summary>
public sealed class MockWolvesvilleClient(ILogger<MockWolvesvilleClient> logger) : IWolvesvilleClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Lock _sync = new();
    private int _shadowfoxFetches;

    public bool IsConfigured => true;

    public string Mode => "Mock";

    public Task<PlayerObservation> GetPlayerByUsernameAsync(string username, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetByUsername(username), cancellationToken);
    }

    public Task<PlayerObservation> GetPlayerByIdAsync(string wolvesvillePlayerId, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetByUsername(UsernameOf(wolvesvillePlayerId)), cancellationToken);
    }

    public Task<IReadOnlyList<ObservedClan>> SearchClansAsync(string name, bool exactName = false, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ObservedClan> result = [.. Clans()
            .Where(c => c.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Observed)];
        return Task.FromResult(result);
    }

    public Task<ObservedClan> GetClanInfoAsync(string wolvesvilleClanId, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        var match = Clans().FirstOrDefault(c => c.Id == wolvesvilleClanId)
            ?? throw new WolvesvilleApiException(404, $"Mock: no clan '{wolvesvilleClanId}'.");
        return Task.FromResult(match.Observed);
    }

    public Task<IReadOnlyList<PlayerObservation>> GetClanMembersAsync(string wolvesvilleClanId, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        var clan = Clans().FirstOrDefault(c => c.Id == wolvesvilleClanId)
            ?? throw new WolvesvilleApiException(404, $"Mock: no clan '{wolvesvilleClanId}'.");

        IReadOnlyList<PlayerObservation> members = [.. clan.MemberUsernames.Select(GetByUsername)];
        return Task.FromResult(members);
    }

    public Task PingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    // --- dataset ---

    private sealed record MockClan(string Id, string Name, string? Description, int Level, long Xps, string[] MemberUsernames)
    {
        public ObservedClan Observed => new(
            Id, Name, Description, MemberUsernames.Length, "en", "inviteOnly", Xps, Level,
            MemberUsernames.Length > 0 ? PlayerIdOf(MemberUsernames[0]) : null,
            [.. MemberUsernames.Select(PlayerIdOf)],
            "mock:GET /clans");
    }

    private static MockClan[] Clans() =>
    [
        new("2001", "Moon Wolves", "Wolves of the silver moon.", 12, 480_000, ["shadowfox", "nightowl", "talon"]),
        new("2002", "Night Watch", "We guard the night.", 9, 210_000, ["wolfsbane"])
    ];

    private static string PlayerIdOf(string username) => username switch
    {
        "shadowfox" => "1001",
        "nightowl" => "1002",
        "wolfsbane" => "1003",
        "talon" => "1004",
        _ => "1000"
    };

    private static string UsernameOf(string wolvesvillePlayerId) => wolvesvillePlayerId switch
    {
        "1001" => "shadowfox",
        "1002" => "nightowl",
        "1003" => "wolfsbane",
        "1004" => "talon",
        _ => throw new WolvesvilleApiException(404, $"Mock: no player '{wolvesvillePlayerId}'.")
    };

    private PlayerObservation GetByUsername(string username)
    {
        var fetches = 0;
        if (username == "shadowfox")
        {
            lock (_sync)
            {
                fetches = ++_shadowfoxFetches;
            }
        }

        (string Id, int Level, int Wins, int Losses, string? ClanId, string[] Badges, string? Message) profile = username switch
        {
            // Scripted drift: clan toggles every fetch, level rises every third fetch, a badge is
            // earned once on the fourth fetch.
            "shadowfox" => ("1001", 80 + fetches / 3, 1_240 + fetches * 3, 610 + fetches,
                fetches % 2 == 1 ? "2001" : "2002",
                fetches >= 4 ? ["badge_hunter", "badge_veteran"] : ["badge_hunter"],
                "Tracking the silver moon."),
            "nightowl" => ("1002", 64, 870, 512, "2001", ["badge_hunter"], "Hoot."),
            "talon" => ("1004", 77, 1_050, 690, "2001", ["badge_veteran"], "Sharp eyes."),
            "wolfsbane" => ("1003", 71, 995, 744, "2002", ["badge_veteran"], null),
            _ => throw new WolvesvilleApiException(404, $"Mock: no player '{username}'.")
        };

        var state = new NormalizedPlayerState
        {
            WolvesvillePlayerId = profile.Id,
            Username = username,
            PersonalMessage = profile.Message,
            Level = profile.Level,
            Status = "ONLINE",
            LastOnline = DateTimeOffset.UtcNow.AddMinutes(-3),
            ClanWolvesvilleId = profile.ClanId,
            Wins = profile.Wins,
            Losses = profile.Losses,
            GamesPlayed = profile.Wins + profile.Losses,
            ReceivedRosesCount = 12,
            SentRosesCount = 4,
            ProfileIconId = "icon_default",
            ProfileIconName = "Default",
            EquippedAvatarId = $"avatar_{profile.Id}",
            BadgeIds = profile.Badges,
            RoleCardIds = ["role_werewolf"],
            RankedSeason = 21,
            RankedWins = 34,
            RankedLosses = 30,
            RankedCurrentRating = 1490 + (profile.Level % 7),
            RankedPlacementRating = 1400,
            Achievements = 18,
            FriendCount = 2
        };

        var raw = JsonSerializer.Serialize(new { state.WolvesvillePlayerId, state.Username, state.Level, state.ClanWolvesvilleId }, Json);
        logger.LogDebug("Mock Wolvesville fetch for {Username} (fetch #{Fetches})", username, fetches);
        return new PlayerObservation(raw, state, "mock:GET /players/{playerId}");
    }
}
