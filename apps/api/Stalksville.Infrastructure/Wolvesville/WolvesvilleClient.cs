using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stalksville.Application;
using Stalksville.Application.Abstractions;
using Stalksville.Application.Models;
using Stalksville.Domain.Models;

namespace Stalksville.Infrastructure.Wolvesville;

/// <summary>
/// Read-only Wolvesville client. Every endpoint here is verified against the official docs at
/// https://api-docs.wolvesville.com/. Responses are normalized into domain state; the client's
/// DTOs never leak into the application layer.
/// </summary>
public sealed class WolvesvilleClient(
    HttpClient http,
    IOptions<WolvesvilleOptions> options,
    ICacheProvider cache,
    ILogger<WolvesvilleClient> logger) : IWolvesvilleClient
{
    private static readonly JsonSerializerOptions ResponseJson = new() { PropertyNameCaseInsensitive = true };

    private WolvesvilleOptions Options => options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Options.ApiKey);

    public string Mode => "Real";

    public async Task<PlayerObservation> GetPlayerByUsernameAsync(string username, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        // Spec: GET /players/search?username={username} returns the exact-match Player profile.
        var observation = await GetPlayerAsync(
            $"players/search?username={Uri.EscapeDataString(username)}",
            $"{CachePolicy.PlayerByKeyPrefix}username:{username.ToLowerInvariant()}",
            bypassCache,
            cancellationToken);
        return observation;
    }

    public async Task<PlayerObservation> GetPlayerByIdAsync(string wolvesvillePlayerId, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        return await GetPlayerAsync(
            $"players/{Uri.EscapeDataString(wolvesvillePlayerId)}",
            $"{CachePolicy.PlayerByKeyPrefix}id:{wolvesvillePlayerId}",
            bypassCache,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ObservedClan>> SearchClansAsync(string name, bool exactName = false, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        var key = $"{CachePolicy.ClanSearchKeyPrefix}{name.ToLowerInvariant()}:{exactName}";

        if (!bypassCache && await cache.GetAsync<IReadOnlyList<ObservedClan>>(key, cancellationToken) is { } cached)
        {
            return cached;
        }

        var path = $"clans/search?name={Uri.EscapeDataString(name)}";
        if (exactName)
        {
            path += "&exactName=true";
        }

        var clans = await GetListAsync<ClanDto>(path, cancellationToken);
        var result = clans.Select(ToObservedClan).ToList();
        await cache.SetAsync(key, result, CachePolicy.ClanSearchTtl, cancellationToken);
        return result;
    }

    public async Task<ObservedClan> GetClanInfoAsync(string wolvesvilleClanId, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        var key = $"{CachePolicy.ClanInfoKeyPrefix}{wolvesvilleClanId}";

        if (!bypassCache && await cache.GetAsync<ObservedClan>(key, cancellationToken) is { } cached)
        {
            return cached;
        }

        var clan = await GetAsync<ClanDto>($"clans/{Uri.EscapeDataString(wolvesvilleClanId)}/info", cancellationToken);
        var result = ToObservedClan(clan);
        await cache.SetAsync(key, result, CachePolicy.ClanTtl, cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<PlayerObservation>> GetClanMembersAsync(string wolvesvilleClanId, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        var key = $"{CachePolicy.ClanMembersKeyPrefix}{wolvesvilleClanId}";

        if (!bypassCache && await cache.GetAsync<IReadOnlyList<PlayerObservation>>(key, cancellationToken) is { } cached)
        {
            return cached;
        }

        var members = await GetListAsync<PlayerProfileDto>($"clans/{Uri.EscapeDataString(wolvesvilleClanId)}/members", cancellationToken);
        // ClanMember payloads have no clanId field: appearing in this response IS the membership
        // observation, so the clan context is injected here with the endpoint as evidence.
        var result = members.Select(m =>
        {
            var obs = ToObservation(m, source: "wolvesville:GET /clans/{clanId}/members");
            return obs with { State = obs.State with { ClanWolvesvilleId = obs.State.ClanWolvesvilleId ?? wolvesvilleClanId } };
        }).ToList();
        await cache.SetAsync(key, result, CachePolicy.ClanTtl, cancellationToken);
        return result;
    }

    public async Task<ObservedHighscores> GetHighscoresAsync(bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        const string cacheKey = "wolvesville:highscores";

        if (!bypassCache && await cache.GetAsync<ObservedHighscores>(cacheKey, cancellationToken) is { } cached)
        {
            logger.LogDebug("Wolvesville cache hit for players/highscores");
            return cached;
        }

        var dto = await GetAsync<HighscoresDto>("players/highscores", cancellationToken);
        var result = new ObservedHighscores(
            [.. dto.AllTime.Select(ToRank)],
            [.. dto.Monthly.Select(ToRank)],
            [.. dto.Weekly.Select(ToRank)],
            [.. dto.Daily.Select(ToRank)],
            "wolvesville:GET /players/highscores");

        await cache.SetAsync(cacheKey, result, CachePolicy.HighscoresTtl, cancellationToken);
        return result;
    }

    private static ObservedHighscoreRank ToRank(HighscoreRankDto rank) => new(rank.PlayerId, rank.Username, rank.Xp);

    public async Task<ObservedRankedLeaderboard> GetRankedLeaderboardAsync(bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        const string cacheKey = "wolvesville:ranked-leaderboard";

        if (!bypassCache && await cache.GetAsync<ObservedRankedLeaderboard>(cacheKey, cancellationToken) is { } cached)
        {
            logger.LogDebug("Wolvesville cache hit for ranked/leaderboard");
            return cached;
        }

        var dto = await GetAsync<LeaderboardDto>("ranked/leaderboard", cancellationToken);
        var result = new ObservedRankedLeaderboard(
            [.. dto.RanksTop.Select(ToRankedEntry)],
            "wolvesville:GET /ranked/leaderboard");

        await cache.SetAsync(cacheKey, result, CachePolicy.RankedLeaderboardTtl, cancellationToken);
        return result;
    }

    private static ObservedRankedEntry ToRankedEntry(RankedLeaderboardPlayerDto player) =>
        new(player.PlayerId, player.Username, player.Skill);

    public async Task<ObservedHallOfFame> GetHallOfFameAsync(int seasonNumber, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"wolvesville:hall-of-fame:{seasonNumber}";

        if (!bypassCache && await cache.GetAsync<ObservedHallOfFame>(cacheKey, cancellationToken) is { } cached)
        {
            logger.LogDebug("Wolvesville cache hit for ranked/hallOfFame/{Season}", seasonNumber);
            return cached;
        }

        var dto = await GetAsync<SeasonWinnersDto>($"ranked/hallOfFame/{seasonNumber}", cancellationToken);
        var result = new ObservedHallOfFame(
            dto.SeasonNumber,
            [.. dto.Winners.Select(w => new ObservedHallOfFameWinner(w.PlayerId, w.PlayerName, w.EquippedAvatar?.Url))],
            $"wolvesville:GET /ranked/hallOfFame/{seasonNumber}");

        await cache.SetAsync(cacheKey, result, CachePolicy.HallOfFameTtl, cancellationToken);
        return result;
    }

    public async Task<ObservedRankedSeason> GetRankedSeasonAsync(bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        const string cacheKey = "wolvesville:ranked-season";

        if (!bypassCache && await cache.GetAsync<ObservedRankedSeason>(cacheKey, cancellationToken) is { } cached)
        {
            logger.LogDebug("Wolvesville cache hit for ranked/season");
            return cached;
        }

        var dto = await GetAsync<RankedSeasonInfoDto>("ranked/season", cancellationToken);
        var result = new ObservedRankedSeason(
            dto.Season.Number,
            dto.Season.StartTime,
            dto.Season.EndTime,
            dto.Season.Finished,
            dto.StartSkillDefault ?? 0,
            "wolvesville:GET /ranked/season");

        await cache.SetAsync(cacheKey, result, CachePolicy.RankedSeasonTtl, cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<ObservedCatalogItem>> GetProfileIconsAsync(bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        const string cacheKey = "wolvesville:catalog:profile-icons";

        if (!bypassCache && await cache.GetAsync<IReadOnlyList<ObservedCatalogItem>>(cacheKey, cancellationToken) is { } cached)
        {
            logger.LogDebug("Wolvesville cache hit for items/profileIcons");
            return cached;
        }

        var dto = await GetAsync<List<ProfileIconDto>>("items/profileIcons", cancellationToken);
        var result = dto.Select(ToCatalogItem).ToList();

        await cache.SetAsync(cacheKey, result, CachePolicy.CatalogTtl, cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<ObservedCatalogItem>> GetBadgesAsync(bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        const string cacheKey = "wolvesville:catalog:badges";

        if (!bypassCache && await cache.GetAsync<IReadOnlyList<ObservedCatalogItem>>(cacheKey, cancellationToken) is { } cached)
        {
            logger.LogDebug("Wolvesville cache hit for items/badges");
            return cached;
        }

        var dto = await GetAsync<List<BadgeDto>>("items/badges?locale=en", cancellationToken);
        var result = dto.Select(b => new ObservedCatalogItem(b.BadgeId, b.Name, b.Rarity, b.Description, b.ImageUrl)).ToList();

        await cache.SetAsync(cacheKey, result, CachePolicy.CatalogTtl, cancellationToken);
        return result;
    }

    private static ObservedCatalogItem ToCatalogItem(ProfileIconDto icon) =>
        new(icon.Id, icon.Name, icon.Rarity, null, icon.ImageUrl);

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        // GET /roles is small and authenticated — used purely as a connectivity/key check.
        // Response shape is irrelevant here, so only the status code is validated.
        await GetRawAsync("roles?locale=en", cancellationToken);
    }

    private async Task<PlayerObservation> GetPlayerAsync(string path, string cacheKey, bool bypassCache, CancellationToken cancellationToken)
    {
        if (!bypassCache && await cache.GetAsync<PlayerObservation>(cacheKey, cancellationToken) is { } cached)
        {
            logger.LogDebug("Wolvesville cache hit for {Path}", path);
            return cached;
        }

        var rawJson = await GetRawAsync(path, cancellationToken);
        var dto = JsonSerializer.Deserialize<PlayerProfileDto>(rawJson, ResponseJson)
            ?? throw new WolvesvilleApiException(500, $"Wolvesville returned an unparseable player payload for {path}.");

        var observation = ToObservation(dto, rawJson);
        await cache.SetAsync(cacheKey, observation, CachePolicy.PlayerTtl, cancellationToken);
        return observation;
    }

    private async Task<string> GetRawAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (body.Length > 400)
            {
                body = body[..400];
            }

            throw new WolvesvilleApiException((int)response.StatusCode, $"Wolvesville API {(int)response.StatusCode} for GET /{path}: {body}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var raw = await GetRawAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<T>(raw, ResponseJson)
            ?? throw new WolvesvilleApiException(500, $"Wolvesville returned an unparseable payload for GET /{path}.");
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    {
        var raw = await GetRawAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<IReadOnlyList<T>>(raw, ResponseJson)
            ?? throw new WolvesvilleApiException(500, $"Wolvesville returned an unparseable payload for GET /{path}.");
    }

    private static PlayerObservation ToObservation(PlayerProfileDto dto, string? rawJson = null, string? source = null)
    {
        var state = new NormalizedPlayerState
        {
            // Full profiles use "id"; clan member payloads use "playerId" (see API spec ClanMember).
            WolvesvillePlayerId = dto.PlayerId ?? dto.Id,
            Username = dto.Username,
            PersonalMessage = dto.PersonalMessage,
            Level = dto.Level,
            // In clan member payloads "status" is the membership status; presence is "playerStatus".
            Status = dto.PlayerId is null ? dto.Status : dto.PlayerStatus ?? dto.Status,
            LastOnline = dto.LastOnline,
            ClanWolvesvilleId = dto.ClanId,
            Wins = dto.GameStats?.TotalWinCount ?? 0,
            Losses = dto.GameStats?.TotalLoseCount ?? 0,
            GamesPlayed = dto.GameStats?.GamesPlayedTotal ?? 0,
            ReceivedRosesCount = dto.ReceivedRosesCount,
            SentRosesCount = dto.SentRosesCount,
            ProfileIconId = dto.ProfileIconId,
            EquippedAvatarId = dto.EquippedAvatar?.Id,
            BadgeIds = dto.BadgeIds ?? [],
            RoleCardIds = dto.RoleCards?.Select(r => r.RoleId1).Where(id => id is not null).ToList() ?? [],
            RankedCurrentRating = dto.RankedSeasonSkill,
            Achievements = dto.GameStats?.Achievements?.Count,
            FriendCount = dto.FriendIds?.Count ?? 0,
            FriendWolvesvilleIds = dto.FriendIds ?? []
        };

        return new PlayerObservation(rawJson ?? JsonSerializer.Serialize(dto, ResponseJson), state, source ?? "wolvesville:GET /players/{playerId}");
    }

    private static ObservedClan ToObservedClan(ClanDto clan) => new(
        clan.Id,
        clan.Name,
        clan.Description,
        clan.MemberCount ?? clan.MemberIds?.Count,
        clan.LanguageCode,
        clan.JoinType,
        clan.Xps,
        clan.Level,
        clan.LeaderId,
        clan.MemberIds ?? [],
        "wolvesville:GET /clans");
}
