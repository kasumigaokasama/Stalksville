using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Stalksville.IntegrationTests;

/// <summary>
/// A real Kestrel server on 127.0.0.1:0 that mirrors the parts of the Wolvesville API used by the
/// vertical slice, including its "Authorization: Bot <key>" + JSON header rules and scripted
/// failures. Test-only control endpoints (/_test/...) mutate state and enqueue failures.
/// </summary>
public sealed class MockWolvesvilleServer : IAsyncDisposable
{
    /// <summary>Random per test run — this mock only ever accepts its own generated key.</summary>
    public static string ApiKey { get; } = $"bot-{Guid.NewGuid():N}";

    private readonly WebApplication _app;

    private readonly Lock _sync = new();
    private readonly Queue<int> _failureQueue = new();
    private readonly Dictionary<string, PlayerFixture> _playersByUsername;
    private readonly Dictionary<string, PlayerFixture> _playersById;
    private readonly Dictionary<string, ClanFixture> _clans;
    private readonly List<HighscoreFixture> _allTimeHighscores;

    public string BaseUrl { get; }

    public MockWolvesvilleServer()
    {
        var flex = new PlayerFixture("3001", "flex", "2001", 42, ["badge_alpha"]);
        var talon = new PlayerFixture("3002", "talon", "2001", 55, []);
        var luna = new PlayerFixture("3003", "luna", "2002", 61, ["badge_beta"]);

        _playersByUsername = new Dictionary<string, PlayerFixture>(StringComparer.OrdinalIgnoreCase)
        {
            [flex.Username] = flex,
            [talon.Username] = talon,
            [luna.Username] = luna
        };
        _playersById = _playersByUsername.Values.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        _clans = new Dictionary<string, ClanFixture>(StringComparer.OrdinalIgnoreCase)
        {
            ["2001"] = new("2001", "Iron Fangs", [flex.Id, talon.Id]),
            ["2002"] = new("2002", "Sky Howl", [luna.Id])
        };

        // Spec shape: GET /players/highscores → { allTime, monthly, weekly, daily } of PlayerRank.
        _allTimeHighscores =
        [
            new("9001", "TopWolf", 500_000),
            new(flex.Id, flex.Username, 400_000),
            new("9002", "QuietStorm", 320_000),
            new(talon.Id, talon.Username, 300_000),
            new("9003", "EchoLeaf", 210_000)
        ];

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();

        MapEndpoints();

        _app.StartAsync().GetAwaiter().GetResult();
        BaseUrl = _app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
            .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.First();
    }

    public HttpClient CreateControlClient() => new() { BaseAddress = new Uri(BaseUrl) };

    private void MapEndpoints()
    {
        _app.MapGet("/roles", (HttpRequest request) => Authorize(request) is { } failed ? failed : Results.Ok(Array.Empty<object>()));

        _app.MapGet("/players/search", (HttpRequest request, string username) =>
            Handle(request, () => _playersByUsername.TryGetValue(username, out var player)
                ? Results.Json(PlayerPayload(player))
                : Results.NotFound(Error($"No player with username {username}"))));

        _app.MapGet("/players/highscores", (HttpRequest request) =>
            Handle(request, () => Results.Json(new
            {
                allTime = SnapshotHighscores(),
                monthly = new object[]
                {
                    new { playerId = "9004", username = "MonthWolf", xp = 40_000L },
                    new { playerId = "9005", username = "LunaRise", xp = 36_000L }
                },
                weekly = new object[]
                {
                    new { playerId = "9006", username = "WeekPaw", xp = 8_000L }
                },
                daily = new object[]
                {
                    new { playerId = "9007", username = "DayHowl", xp = 900L }
                }
            })));

        _app.MapGet("/players/{playerId}", (string playerId, HttpRequest request) =>
            Handle(request, () => _playersById.TryGetValue(playerId, out var player)
                ? Results.Json(PlayerPayload(player))
                : Results.NotFound(Error($"No player with id {playerId}"))));

        _app.MapGet("/clans/search", (HttpRequest request, string name, bool? exactName) =>
            Handle(request, () =>
            {
                var matches = _clans.Values.Where(c => exactName == true
                    ? c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    : c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                return Results.Json(matches.Select(ClanPayload).ToList());
            }));

        _app.MapGet("/clans/{clanId}/info", (string clanId, HttpRequest request) =>
            Handle(request, () => _clans.TryGetValue(clanId, out var clan)
                ? Results.Json(ClanPayload(clan))
                : Results.NotFound(Error($"No clan with id {clanId}"))));

        _app.MapGet("/clans/{clanId}/members", (string clanId, HttpRequest request) =>
            Handle(request, () => _clans.TryGetValue(clanId, out var clan)
                // Spec shape: clan members are ClanMember objects keyed by "playerId", not full profiles.
                ? Results.Json(clan.MemberIds.Where(_playersById.ContainsKey).Select(id => MemberPayload(_playersById[id])).ToList())
                : Results.NotFound(Error($"No clan with id {clanId}"))));

        // ---- test-only control endpoints (no auth; never mirrored from the real API) ----

        _app.MapPut("/_test/players/{username}", (string username, PlayerMutation mutation) =>
        {
            lock (_sync)
            {
                if (!_playersByUsername.TryGetValue(username, out var player))
                {
                    return Results.NotFound();
                }

                if (mutation.ClanId is not null)
                {
                    player.ClanId = mutation.ClanId == "" ? null : mutation.ClanId;
                }

                if (mutation.Level is not null)
                {
                    player.Level = mutation.Level.Value;
                }

                if (mutation.AddBadge is not null)
                {
                    player.BadgeIds = [.. player.BadgeIds, mutation.AddBadge];
                }

                if (mutation.Wins is not null)
                {
                    player.Wins = mutation.Wins.Value;
                }
            }

            return Results.Ok();
        });

        _app.MapPut("/_test/highscores", (HighscoreMutation mutation) =>
        {
            lock (_sync)
            {
                var entry = _allTimeHighscores.FirstOrDefault(h => h.Username.Equals(mutation.Username, StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    return Results.NotFound();
                }

                var target = Math.Clamp(mutation.NewRank, 1, _allTimeHighscores.Count);
                _allTimeHighscores.Remove(entry);
                _allTimeHighscores.Insert(target - 1, entry);
            }

            return Results.Ok();
        });

        _app.MapPost("/_test/failures", (FailureScript script) =>
        {
            lock (_sync)
            {
                for (var i = 0; i < script.Count; i++)
                {
                    _failureQueue.Enqueue(script.Status);
                }
            }

            return Results.Ok();
        });
    }

    private IResult Handle(HttpRequest request, Func<IResult> handle)
    {
        if (Authorize(request) is { } failed)
        {
            return failed;
        }

        int? scripted = null;
        lock (_sync)
        {
            if (_failureQueue.Count > 0)
            {
                scripted = _failureQueue.Dequeue();
            }
        }

        if (scripted is { } status)
        {
            return Results.StatusCode(status);
        }

        return handle();
    }

    private static IResult? Authorize(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var auth) || auth.ToString() != $"Bot {ApiKey}")
        {
            return Results.Unauthorized();
        }

        if (!request.Headers.TryGetValue("Accept", out var accept) || !accept.ToString().Contains("application/json"))
        {
            return Results.StatusCode(StatusCodes.Status406NotAcceptable);
        }

        return null;
    }

    private static object PlayerPayload(PlayerFixture player) => new
    {
        id = player.Id,
        username = player.Username,
        personalMessage = (string?)null,
        level = player.Level,
        status = "ONLINE",
        lastOnline = "2026-01-01T00:00:00Z", // static: identical re-fetches must produce identical hashes
        clanId = player.ClanId,
        rankedSeasonSkill = 1500,
        rankedSeasonPlayedCount = 18,
        receivedRosesCount = 3,
        sentRosesCount = 1,
        profileIconId = "icon_default",
        equippedAvatar = new { id = $"avatar_{player.Id}", name = "Base" },
        badgeIds = player.BadgeIds,
        roleCards = new[] { new { roleId1 = "role_seer", rarity = "COMMON" } },
        gameStats = new
        {
            totalWinCount = player.Wins,
            totalLoseCount = 200,
            totalTieCount = 0,
            achievements = new[] { new { roleId = "role_seer", level = 4, points = 85, pointsNextLevel = 100, category = "EASY" } }
        },
        friendIds = Array.Empty<string>()
    };

    /// <summary>ClanMember payload per spec: "playerId", membership "status", "playerStatus" for presence.</summary>
    private static object MemberPayload(PlayerFixture player) => new
    {
        playerId = player.Id,
        creationTime = "2020-01-01T00:00:00Z",
        xp = 1000L,
        status = "ACCEPTED",
        isCoLeader = false,
        username = player.Username,
        level = player.Level,
        lastOnline = "2026-01-01T00:00:00Z",
        profileIconId = "icon_default",
        playerStatus = "DEFAULT"
    };

    private static object ClanPayload(ClanFixture clan) => new
    {
        id = clan.Id,
        name = clan.Name,
        description = "mock clan",
        memberCount = clan.MemberIds.Count,
        languageCode = "en",
        joinType = "inviteOnly",
        xps = 100_000L,
        level = 5,
        iconId = "icon",
        leaderId = clan.MemberIds.FirstOrDefault(),
        memberIds = clan.MemberIds
    };

    private static object Error(string message) => new { message, status = 404 };

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private List<object> SnapshotHighscores()
    {
        lock (_sync)
        {
            return _allTimeHighscores.Select(h => (object)new { playerId = h.PlayerId, username = h.Username, xp = h.Xp }).ToList();
        }
    }

    private sealed record PlayerFixture(string Id, string Username, string? ClanId, int Level, string[] BadgeIds)
    {
        public string? ClanId { get; set; } = ClanId;

        public int Level { get; set; } = Level;

        public string[] BadgeIds { get; set; } = BadgeIds;

        public int Wins { get; set; } = 400;
    }

    private sealed record ClanFixture(string Id, string Name, List<string> MemberIds);

    private sealed record PlayerMutation(string? ClanId, int? Level, string? AddBadge, int? Wins);

    private sealed record FailureScript(int Status, int Count);

    private sealed record HighscoreMutation(string Username, int NewRank);

    private sealed record HighscoreFixture(string PlayerId, string Username, long Xp);
}
