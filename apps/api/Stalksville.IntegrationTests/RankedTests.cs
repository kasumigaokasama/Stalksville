using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>Ranked ingestion: capture stores the board, matches tracked players, derives rank shifts; catalogs enrich dossiers.</summary>
public sealed class RankedTests : IAsyncLifetime
{
    /// <summary>The mock board has only 5 rows, so this suite tightens the rank-shift threshold.</summary>
    private sealed class RankedTestFactory : StalksvilleApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Alerts:RankShiftThreshold"] = "3"
            }));
        }
    }

    private RankedTestFactory _factory = null!;
    private string _token = null!;
    private HttpClient _client = null!;
    private HttpClient _mockControl = null!;

    public async Task InitializeAsync()
    {
        _factory = new RankedTestFactory();
        _token = await AuthTests.LoginAsync(_factory);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", _token);
        _mockControl = _factory.Mock.CreateControlClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _mockControl.Dispose();
        await _factory.DisposeAllAsync();
    }

    [Fact]
    public async Task Capture_StoresBoardWithSeasonAndMatchesTrackedPlayers()
    {
        // Track flex first so the capture can resolve the leaderboard row to a dossier.
        var lookup = await _client.GetAsync("/api/v1/players/lookup?username=flex");
        lookup.EnsureSuccessStatusCode();

        var capture = await _client.PostAsync("/api/v1/ranked/capture", content: null);
        Assert.True(capture.IsSuccessStatusCode);
        var captureBody = JsonDocument.Parse(await capture.Content.ReadAsStreamAsync()).RootElement;
        Assert.Equal(5, captureBody.GetProperty("entriesStored").GetInt32());
        Assert.Equal(21, captureBody.GetProperty("seasonNumber").GetInt32());
        Assert.Equal(0, captureBody.GetProperty("rankShiftAlerts").GetInt32()); // first capture has no baseline

        var board = await GetJsonAsync("/api/v1/ranked");
        Assert.Equal(21, board.GetProperty("seasonNumber").GetInt32());
        var rows = board.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(5, rows.Count);

        var flexRow = rows.Single(r => r.GetProperty("username").GetString() == "flex");
        Assert.Equal(4, flexRow.GetProperty("rank").GetInt32());
        Assert.True(flexRow.GetProperty("tracked").GetBoolean());

        var season = await GetJsonAsync("/api/v1/ranked/season");
        Assert.Equal(21, season.GetProperty("number").GetInt32());
        Assert.False(season.GetProperty("finished").GetBoolean());
    }

    [Fact]
    public async Task RankShift_BetweenCaptures_RaisesAlertAndTimelineEvent()
    {
        var lookup = await _client.GetAsync("/api/v1/players/lookup?username=luna");
        lookup.EnsureSuccessStatusCode();

        // Baseline capture: luna sits at rank 2.
        var first = await _client.PostAsync("/api/v1/ranked/capture", content: null);
        first.EnsureSuccessStatusCode();

        // Captures are keyed by UtcNow; spacing them out keeps "previous capture" resolvable.
        await Task.Delay(20);

        // A three-place demotion crosses the threshold (3).
        var demote = await _mockControl.PutAsJsonAsync("/_test/ranked", new { username = "luna", newRank = 5, skillDelta = -60 });
        demote.EnsureSuccessStatusCode();
        var second = await _client.PostAsync("/api/v1/ranked/capture", content: null);
        var secondBody = JsonDocument.Parse(await second.Content.ReadAsStreamAsync()).RootElement;
        Assert.Equal(1, secondBody.GetProperty("rankShiftAlerts").GetInt32());

        var alerts = await GetJsonAsync("/api/v1/alerts?kind=RankShift");
        var alert = Assert.Single(alerts.EnumerateArray());
        Assert.Equal("luna", alert.GetProperty("entityTitle").GetString());
        Assert.Contains("dropped 3 rank(s)", alert.GetProperty("title").GetString());
        Assert.Contains("ranked", alert.GetProperty("title").GetString());
        Assert.Contains("2 → 5", alert.GetProperty("body").GetString());

        // The derived event lands on the global timeline with its own event type.
        var timeline = await GetJsonAsync("/api/v1/timeline?eventType=RankedRankChanged");
        var events = timeline.EnumerateArray().ToList();
        Assert.Contains(events, e => e.GetProperty("summary").GetString()!.Contains("2 → 5"));
    }

    [Fact]
    public async Task Catalog_LazilyRefreshesAndEnrichesDossier()
    {
        // The catalog endpoint lazily pulls /items/* on first call.
        var badges = await GetJsonAsync("/api/v1/catalog?kind=badge");
        var badgeItems = badges.EnumerateArray().ToList();
        Assert.Contains(badgeItems, i => i.GetProperty("externalId").GetString() == "badge_alpha"
            && i.GetProperty("name").GetString() == "Alpha Hunter");

        var icons = await GetJsonAsync("/api/v1/catalog?kind=profileIcon");
        Assert.Contains(icons.EnumerateArray(), i => i.GetProperty("externalId").GetString() == "icon_default"
            && i.GetProperty("name").GetString() == "Default");

        // Unknown kinds are rejected.
        var bad = await _client.GetAsync("/api/v1/catalog?kind=talisman");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // The dossier resolves the profile icon id against the refreshed catalog.
        var lookup = await _client.GetAsync("/api/v1/players/lookup?username=flex");
        lookup.EnsureSuccessStatusCode();
        var dossier = JsonDocument.Parse(await lookup.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("dossier").GetProperty("observed");
        Assert.Equal("Default", dossier.GetProperty("profileIconName").GetString());
    }

    [Fact]
    public async Task Capture_RequiresAnalystRole()
    {
        var viewerToken = await CreateViewerAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ranked/capture");
        request.Headers.Authorization = new("Bearer", viewerToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HallOfFame_CaptureStoresWinnersAndAlertsTrackedPlayers()
    {
        // Track luna first so the capture resolves her winner row to a dossier.
        var lookup = await _client.GetAsync("/api/v1/players/lookup?username=luna");
        lookup.EnsureSuccessStatusCode();

        var capture = await _client.PostAsync("/api/v1/ranked/hall-of-fame/capture?season=20", content: null);
        Assert.True(capture.IsSuccessStatusCode);
        var captureBody = JsonDocument.Parse(await capture.Content.ReadAsStreamAsync()).RootElement;
        Assert.Equal(20, captureBody.GetProperty("seasonNumber").GetInt32());
        Assert.Equal(3, captureBody.GetProperty("entriesStored").GetInt32());
        Assert.Equal(1, captureBody.GetProperty("trackedWinnerAlerts").GetInt32());

        var board = await GetJsonAsync("/api/v1/ranked/hall-of-fame");
        Assert.Equal(20, board.GetProperty("seasonNumber").GetInt32());
        var seasons = board.GetProperty("availableSeasons").EnumerateArray().Select(s => s.GetInt32()).ToList();
        Assert.Equal([20], seasons);

        var rows = board.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(3, rows.Count);
        var lunaRow = rows.Single(r => r.GetProperty("playerName").GetString() == "luna");
        Assert.Equal(2, lunaRow.GetProperty("position").GetInt32());
        Assert.True(lunaRow.GetProperty("tracked").GetBoolean());
        Assert.NotNull(lunaRow.GetProperty("avatarUrl").GetString());

        var alerts = await GetJsonAsync("/api/v1/alerts?kind=HallOfFameEntry");
        var alert = Assert.Single(alerts.EnumerateArray());
        Assert.Equal("luna", alert.GetProperty("entityTitle").GetString());
        Assert.Contains("season 20", alert.GetProperty("title").GetString());
    }

    [Fact]
    public async Task HallOfFame_UnknownSeason_MapsUpstream404()
    {
        var response = await _client.PostAsync("/api/v1/ranked/hall-of-fame/capture?season=19", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;
        Assert.Equal("upstream", body.GetProperty("type").GetString());
    }

    private async Task<string> CreateViewerAsync()
    {
        var viewerUsername = $"viewer-{Guid.NewGuid():N}"[..32];
        var viewerPassword = $"viewer-{Guid.NewGuid():N}";
        var created = await _client.PostAsJsonAsync("/api/v1/admin/users",
            new { username = viewerUsername, password = viewerPassword, role = "VIEWER" });
        created.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { username = viewerUsername, password = viewerPassword });
        login.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await login.Content.ReadAsStreamAsync()).RootElement.GetProperty("token").GetString()!;
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;
    }
}
