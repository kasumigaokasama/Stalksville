using Stalksville.Application.Models;
using Stalksville.Domain.Models;

namespace Stalksville.Application.Abstractions;

/// <summary>
/// Read-only port to the Wolvesville API. No write operations exist by design; adding any would
/// require an explicit product decision plus a feature flag.
/// </summary>
public interface IWolvesvilleClient
{
    /// <summary>Whether an API key is configured (real mode). Mock mode reports true.</summary>
    bool IsConfigured { get; }

    /// <summary>"Real" or "Mock".</summary>
    string Mode { get; }

    /// <summary>Exact-username player lookup: GET /players/search?username={username}.</summary>
    Task<PlayerObservation> GetPlayerByUsernameAsync(string username, bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>Player profile by Wolvesville id: GET /players/{playerId}.</summary>
    Task<PlayerObservation> GetPlayerByIdAsync(string wolvesvillePlayerId, bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>Clan search: GET /clans/search?name=….</summary>
    Task<IReadOnlyList<ObservedClan>> SearchClansAsync(string name, bool exactName = false, bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>Clan info: GET /clans/{clanId}/info.</summary>
    Task<ObservedClan> GetClanInfoAsync(string wolvesvilleClanId, bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>Clan members: GET /clans/{clanId}/members (ClanMember payloads).</summary>
    Task<IReadOnlyList<PlayerObservation>> GetClanMembersAsync(string wolvesvilleClanId, bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>Top-100 XP boards: GET /players/highscores (allTime/monthly/weekly/daily).</summary>
    Task<ObservedHighscores> GetHighscoresAsync(bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>Ranked leaderboard: GET /ranked/leaderboard (ranksTop; rank is array order).</summary>
    Task<ObservedRankedLeaderboard> GetRankedLeaderboardAsync(bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>Current ranked season: GET /ranked/season (season number + window).</summary>
    Task<ObservedRankedSeason> GetRankedSeasonAsync(bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>Profile icon catalog: GET /items/profileIcons (short ids → names).</summary>
    Task<IReadOnlyList<ObservedCatalogItem>> GetProfileIconsAsync(bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>Badge catalog: GET /items/badges?locale=en (short ids → names).</summary>
    Task<IReadOnlyList<ObservedCatalogItem>> GetBadgesAsync(bool bypassCache = false, CancellationToken cancellationToken = default);

    /// <summary>Cheap authenticated call (GET /roles) used for connectivity checks. Throws on failure.</summary>
    Task PingAsync(CancellationToken cancellationToken = default);
}
