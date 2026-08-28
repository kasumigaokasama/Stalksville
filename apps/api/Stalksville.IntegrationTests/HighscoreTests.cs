using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>Highscore ingestion: capture stores boards, matches tracked players, derives rank shifts.</summary>
public sealed class HighscoreTests : IAsyncLifetime
{
    /// <summary>The mock board has only 5 rows, so this suite tightens the rank-shift threshold.</summary>
    private sealed class HighscoreTestFactory : StalksvilleApiFactory
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

    private HighscoreTestFactory _factory = null!;
    private string _token = null!;
    private HttpClient _client = null!;
    private HttpClient _mockControl = null!;

    public async Task InitializeAsync()
    {
        _factory = new HighscoreTestFactory();
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
    public async Task Capture_StoresBoardsAndMatchesTrackedPlayers()
    {
        // Track flex first so the capture can resolve the leaderboard row to a dossier.
        var lookup = await _client.GetAsync("/api/v1/players/lookup?username=flex");
        lookup.EnsureSuccessStatusCode();

        var capture = await _client.PostAsync("/api/v1/highscores/capture", content: null);
        var captureBody = JsonDocument.Parse(await capture.Content.ReadAsStreamAsync()).RootElement;
        Assert.True(capture.IsSuccessStatusCode);
        Assert.True(captureBody.GetProperty("entriesStored").GetInt32() > 0);
        Assert.Equal(0, captureBody.GetProperty("rankShiftAlerts").GetInt32()); // first capture has no baseline

        var board = await GetBoardAsync("alltime");
        Assert.Equal(5, board.Rows.Count);

        var flexRow = board.Rows.Single(r => r.Username == "flex");
        Assert.Equal(2, flexRow.Rank);
        Assert.True(flexRow.Tracked);
        Assert.NotNull(flexRow.PlayerId);

        // Untracked rows keep their Wolvesville identity.
        var topRow = board.Rows.Single(r => r.Username == "TopWolf");
        Assert.False(topRow.Tracked);
        Assert.Equal("9001", topRow.WolvesvillePlayerId);
    }

    [Fact]
    public async Task RankShift_BetweenCaptures_RaisesAlertAndTimelineEvent()
    {
        var lookup = await _client.GetAsync("/api/v1/players/lookup?username=talon");
        lookup.EnsureSuccessStatusCode();
        var talonId = JsonDocument.Parse(await lookup.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("dossier").GetProperty("player").GetProperty("id").GetGuid();

        // Baseline capture: talon sits at rank 4.
        var first = await _client.PostAsync("/api/v1/highscores/capture", content: null);
        first.EnsureSuccessStatusCode();

        // Captures are keyed by UtcNow; spacing them out keeps "previous capture" resolvable.
        await Task.Delay(20);

        // A one-place demotion stays below the threshold (3).
        var demote = await _mockControl.PutAsJsonAsync("/_test/highscores", new { username = "talon", newRank = 5 });
        demote.EnsureSuccessStatusCode();
        var second = await _client.PostAsync("/api/v1/highscores/capture", content: null);
        var secondBody = JsonDocument.Parse(await second.Content.ReadAsStreamAsync()).RootElement;
        Assert.Equal(0, secondBody.GetProperty("rankShiftAlerts").GetInt32());

        await Task.Delay(20);

        // A four-place climb crosses it.
        var promote = await _mockControl.PutAsJsonAsync("/_test/highscores", new { username = "talon", newRank = 1 });
        promote.EnsureSuccessStatusCode();
        var third = await _client.PostAsync("/api/v1/highscores/capture", content: null);
        var thirdRaw = await third.Content.ReadAsStringAsync();
        var thirdBody = JsonDocument.Parse(thirdRaw).RootElement;
        Assert.Equal(1, thirdBody.GetProperty("rankShiftAlerts").GetInt32());

        var alerts = await GetJsonAsync("/api/v1/alerts?kind=RankShift");
        var alert = Assert.Single(alerts.EnumerateArray());
        Assert.Equal("talon", alert.GetProperty("entityTitle").GetString());
        Assert.Contains("climbed 4 rank(s)", alert.GetProperty("title").GetString());
        Assert.Contains("5 → 1", alert.GetProperty("body").GetString());

        // The derived event lands on the global timeline.
        var timeline = await GetJsonAsync("/api/v1/timeline?eventType=HighscoreRankChanged");
        var events = timeline.EnumerateArray().ToList();
        Assert.Contains(events, e => e.GetProperty("summary").GetString()!.Contains("5 → 1"));
    }

    [Fact]
    public async Task BoardQuery_ValidatesPeriod()
    {
        var response = await _client.GetAsync("/api/v1/highscores?period=yearly");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Capture_RequiresAnalystRole()
    {
        var viewerToken = await CreateViewerAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/highscores/capture");
        request.Headers.Authorization = new("Bearer", viewerToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record BoardRow(int Rank, string Username, string WolvesvillePlayerId, long Xp, Guid? PlayerId, bool Tracked);

    private sealed record Board(string Period, DateTimeOffset? CapturedAt, List<BoardRow> Rows);

    private async Task<Board> GetBoardAsync(string period)
    {
        var json = await GetJsonAsync($"/api/v1/highscores?period={period}");
        return new Board(
            json.GetProperty("period").GetString()!,
            json.GetProperty("capturedAt").GetDateTimeOffset(),
            json.GetProperty("rows").EnumerateArray().Select(r => new BoardRow(
                r.GetProperty("rank").GetInt32(),
                r.GetProperty("username").GetString()!,
                r.GetProperty("wolvesvillePlayerId").GetString()!,
                r.GetProperty("xp").GetInt64(),
                r.TryGetProperty("playerId", out var pid) && pid.ValueKind != JsonValueKind.Null ? pid.GetGuid() : null,
                r.GetProperty("tracked").GetBoolean())).ToList());
    }

    private async Task<string> CreateViewerAsync()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/admin/users", new { username = "spectator-hs", password = "spectator-pass-1", role = "VIEWER" });
        Assert.True(created.IsSuccessStatusCode || created.StatusCode == HttpStatusCode.Conflict);

        var login = await _client.PostAsync("/api/v1/auth/login", JsonContent.Create(new { username = "spectator-hs", password = "spectator-pass-1" }));
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
