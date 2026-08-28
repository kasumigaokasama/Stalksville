using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>
/// Intelligence depth: friend-network materialization (observed friendIds → FRIEND_OF
/// relationships between tracked players) and persisted exposure assessments with shift alerts.
/// </summary>
public sealed class FriendAndExposureTests : IAsyncLifetime
{
    /// <summary>Tightens the exposure-shift threshold so the scripted clan change crosses it.</summary>
    private sealed class FriendTestFactory : StalksvilleApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Alerts:ExposureShiftThreshold"] = "5"
            }));
        }
    }

    private FriendTestFactory _factory = null!;
    private string _token = null!;
    private HttpClient _client = null!;
    private HttpClient _mockControl = null!;

    public async Task InitializeAsync()
    {
        _factory = new FriendTestFactory();
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
    public async Task TrackedFriends_AreMaterializedBothWays_AndSurfaceInTheDossier()
    {
        // flex and talon are seeded as friends in the mock server.
        await _client.GetAsync("/api/v1/players/lookup?username=flex");
        await _client.GetAsync("/api/v1/players/lookup?username=talon");

        var flexDossier = await GetJsonAsync("/api/v1/players?query=flex");
        // The list returns summaries; use the id to fetch the full dossier.
        var flexId = flexDossier.EnumerateArray().Single(p => p.GetProperty("username").GetString() == "flex").GetProperty("id").GetString();
        var dossier = await GetJsonAsync($"/api/v1/players/{flexId}");

        var friends = dossier.GetProperty("derived").GetProperty("friends").EnumerateArray().ToList();
        var talonFriend = Assert.Single(friends, f => f.GetProperty("username").GetString() == "talon");
        Assert.True(talonFriend.GetProperty("current").GetBoolean());

        var relationships = dossier.GetProperty("derived").GetProperty("relationships").EnumerateArray().ToList();
        Assert.Contains(relationships, r => r.GetProperty("type").GetString() == "FRIEND_OF");

        // The graph carries the player↔player edge.
        var graph = await GetJsonAsync("/api/v1/graph");
        Assert.Contains(graph.GetProperty("edges").EnumerateArray(),
            e => e.GetProperty("type").GetString() == "FRIEND_OF");
    }

    [Fact]
    public async Task NewFriendLinkBetweenTrackedPlayers_RaisesAlertAndClosesOnRemoval()
    {
        await _client.GetAsync("/api/v1/players/lookup?username=flex");
        var firstLuna = await _client.GetAsync("/api/v1/players/lookup?username=luna");
        firstLuna.EnsureSuccessStatusCode();
        var lunaId = JsonDocument.Parse(await firstLuna.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("dossier").GetProperty("player").GetProperty("id").GetString();

        // luna (tracked) adds flex (tracked) — mock id 3001. Refresh (not lookup) so the
        // client-side player cache doesn't serve the pre-mutation payload.
        var mutate = await _mockControl.PutAsJsonAsync("/_test/players/luna", new { addFriend = "3001" });
        mutate.EnsureSuccessStatusCode();

        await _client.PostAsync($"/api/v1/players/{lunaId}/refresh", content: null);

        var alerts = await GetJsonAsync("/api/v1/alerts?kind=FriendLinkAdded");
        var alert = Assert.Single(alerts.EnumerateArray());
        Assert.Equal("luna", alert.GetProperty("entityTitle").GetString());
        Assert.Contains("flex", alert.GetProperty("title").GetString());

        var timeline = await GetJsonAsync("/api/v1/timeline?eventType=FriendshipChanged");
        Assert.Contains(timeline.EnumerateArray(), e => e.GetProperty("summary").GetString()!.Contains("Friend list changed"));

        // Removing the friend closes the relationship in both directions.
        var remove = await _mockControl.PutAsJsonAsync("/_test/players/luna", new { removeFriend = "3001" });
        remove.EnsureSuccessStatusCode();
        await _client.PostAsync($"/api/v1/players/{lunaId}/refresh", content: null);

        var dossier = await GetJsonAsync($"/api/v1/players/{lunaId}");
        var flexFriend = dossier.GetProperty("derived").GetProperty("friends").EnumerateArray()
            .Single(f => f.GetProperty("username").GetString() == "flex");
        Assert.False(flexFriend.GetProperty("current").GetBoolean());
    }

    [Fact]
    public async Task ExposureShift_AfterClanChange_RaisesAlertAndTimelineEvent()
    {
        var lookup = await _client.GetAsync("/api/v1/players/lookup?username=flex");
        lookup.EnsureSuccessStatusCode();
        var flexId = JsonDocument.Parse(await lookup.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("dossier").GetProperty("player").GetProperty("id").GetString();

        // Baseline exposure exists after the first import; moving clans changes several
        // categories at once (clan + historical + network), crossing the tightened threshold.
        var mutate = await _mockControl.PutAsJsonAsync("/_test/players/flex", new { clanId = "2002" });
        mutate.EnsureSuccessStatusCode();

        var refresh = await _client.PostAsync($"/api/v1/players/{flexId}/refresh", content: null);
        refresh.EnsureSuccessStatusCode();

        var alerts = await GetJsonAsync("/api/v1/alerts?kind=ExposureShift");
        var alert = Assert.Single(alerts.EnumerateArray());
        Assert.Equal("flex", alert.GetProperty("entityTitle").GetString());
        Assert.Contains("exposure", alert.GetProperty("title").GetString(), StringComparison.OrdinalIgnoreCase);

        var timeline = await GetJsonAsync("/api/v1/timeline?eventType=ExposureShifted");
        var exposureEvent = Assert.Single(timeline.EnumerateArray());
        Assert.Contains("→", exposureEvent.GetProperty("summary").GetString());

        // Evidence points at the two assessments that were diffed.
        Assert.Contains("before", alert.GetProperty("evidence").GetRawText());
        Assert.Contains("after", alert.GetProperty("evidence").GetRawText());
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;
    }
}
