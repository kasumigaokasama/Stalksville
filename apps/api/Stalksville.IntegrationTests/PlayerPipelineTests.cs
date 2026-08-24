using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>End-to-end player pipeline: lookup → import → smart snapshot → change detection → memberships.</summary>
public sealed class PlayerPipelineTests : IAsyncLifetime
{
    private StalksvilleApiFactory _factory = null!;
    private string _token = null!;
    private HttpClient _client = null!;
    private HttpClient _mockControl = null!;

    public async Task InitializeAsync()
    {
        _factory = new StalksvilleApiFactory();
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
    public async Task Lookup_ImportsPlayerAndCreatesSnapshot()
    {
        var response = await _client.GetAsync("/api/v1/players/lookup?username=flex");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var dossier = body.RootElement.GetProperty("dossier");

        Assert.Equal("flex", dossier.GetProperty("player").GetProperty("username").GetString());
        Assert.Equal("3001", dossier.GetProperty("observed").GetProperty("wolvesvillePlayerId").GetString());
        Assert.Equal("2001", dossier.GetProperty("observed").GetProperty("clanWolvesvilleId").GetString());
        Assert.NotEqual(default, dossier.GetProperty("player").GetProperty("currentClanId").GetGuid());
        Assert.False(body.RootElement.GetProperty("wasReobserved").GetBoolean());
    }

    [Fact]
    public async Task Refresh_WithUnchangedState_ReobservesWithoutNewSnapshot()
    {
        var lookup = await LookupAsync("talon");
        var playerId = lookup.GetProperty("dossier").GetProperty("player").GetProperty("id").GetGuid();

        var refresh = await RefreshAsync(playerId);

        Assert.True(refresh.GetProperty("wasReobserved").GetBoolean());
        Assert.Equal(0, refresh.GetProperty("changesDetectedInThisObservation").GetInt32());

        var snapshots = await GetJsonAsync($"/api/v1/players/{playerId}/snapshots");
        var snapshotList = snapshots.EnumerateArray().ToList();
        var snapshot = Assert.Single(snapshotList);
        Assert.Equal(2, snapshot.GetProperty("observationCount").GetInt32());
    }

    [Fact]
    public async Task Refresh_AfterClanChange_DetectsChangeAndTransitionsMembership()
    {
        var lookup = await LookupAsync("flex");
        var playerId = lookup.GetProperty("dossier").GetProperty("player").GetProperty("id").GetGuid();

        // The mock player switches clans upstream.
        var mutate = await _mockControl.PutAsJsonAsync("/_test/players/flex", new { clanId = "2002", level = 43 });
        mutate.EnsureSuccessStatusCode();

        var refresh = await RefreshAsync(playerId);

        Assert.False(refresh.GetProperty("wasReobserved").GetBoolean());
        Assert.True(refresh.GetProperty("changesDetectedInThisObservation").GetInt32() >= 2); // clanId + level

        var changes = await GetJsonAsync($"/api/v1/players/{playerId}/changes");
        var clanChange = changes.EnumerateArray().Single(c => c.GetProperty("field").GetString() == "clanId");
        Assert.Equal("2001", clanChange.GetProperty("oldValue").GetString());
        Assert.Equal("2002", clanChange.GetProperty("newValue").GetString());

        // Evidence must trace back to the snapshots that prove the change.
        var evidence = clanChange.GetProperty("evidence").EnumerateArray().ToList();
        Assert.Equal(2, evidence.Count);
        Assert.All(evidence, e => Assert.False(string.IsNullOrEmpty(e.GetProperty("sourceReference").GetString())));

        // Membership history: 2001 ended, 2002 current.
        var dossier = await GetJsonAsync($"/api/v1/players/{playerId}");
        var memberships = dossier.GetProperty("derived").GetProperty("memberships").EnumerateArray().ToList();
        Assert.Equal(2, memberships.Count);
        Assert.Contains(memberships, m => m.GetProperty("wolvesvilleClanId").GetString() == "2002" && m.GetProperty("isCurrent").GetBoolean());
        Assert.Contains(memberships, m => m.GetProperty("wolvesvilleClanId").GetString() == "2001" && !m.GetProperty("isCurrent").GetBoolean());

        // Relationship flipped to the new clan.
        var relationships = dossier.GetProperty("derived").GetProperty("relationships").EnumerateArray().ToList();
        var memberOf = Assert.Single(relationships, r => r.GetProperty("isCurrent").GetBoolean());
        Assert.Equal("MEMBER_OF", memberOf.GetProperty("type").GetString());
        Assert.Equal(1.0, memberOf.GetProperty("confidence").GetDouble());

        // The timeline has derived events for the clan switch.
        var timeline = await GetJsonAsync("/api/v1/system/timeline");
        Assert.Contains(timeline.EnumerateArray(), e => e.GetProperty("eventType").GetString() == "ClanChanged");
    }

    [Fact]
    public async Task Lookup_UnknownUsername_MapsUpstream404ToNotFound()
    {
        var response = await _client.GetAsync("/api/v1/players/lookup?username=ghost");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClanImport_SnapshotsAllMembersAndTracksMemberships()
    {
        var import = await _client.PostAsync("/api/v1/clans/2001/import", content: null);

        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var body = JsonDocument.Parse(await import.Content.ReadAsStreamAsync());

        Assert.Equal("Iron Fangs", body.RootElement.GetProperty("clan").GetProperty("name").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("openMembershipCount").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("knownMembers").GetArrayLength());

        var members = body.RootElement.GetProperty("knownMembers").EnumerateArray().ToList();
        Assert.Contains(members, m => m.GetProperty("username").GetString() == "flex");
        Assert.Contains(members, m => m.GetProperty("username").GetString() == "talon");

        // Regression for live-API member imports: members arrive as ClanMember objects keyed by
        // "playerId" — every member must land in its own tracked player row, never merge into one.
        var tracked = await GetJsonAsync("/api/v1/players");
        var trackedList = tracked.EnumerateArray().ToList();
        Assert.Contains(trackedList, p => p.GetProperty("wolvesvillePlayerId").GetString() == "3001"
            && p.GetProperty("username").GetString() == "flex");
        Assert.Contains(trackedList, p => p.GetProperty("wolvesvillePlayerId").GetString() == "3002"
            && p.GetProperty("username").GetString() == "talon");
        Assert.Equal(2, trackedList.Select(p => p.GetProperty("id").GetString()).Distinct().Count());
        Assert.All(trackedList, p => Assert.False(string.IsNullOrEmpty(p.GetProperty("wolvesvillePlayerId").GetString())));
    }

    [Fact]
    public async Task WolvesvilleStatus_ReportsConnectedInRealMode()
    {
        var status = await GetJsonAsync("/api/v1/system/wolvesville/status");

        Assert.Equal("Real", status.GetProperty("mode").GetString());
        Assert.Equal("Connected", status.GetProperty("status").GetString());
        Assert.False(status.GetProperty("writeOperationsEnabled").GetBoolean());
        Assert.Contains(status.GetProperty("capabilities").EnumerateArray(),
            c => c.GetProperty("name").GetString() == "Write operations" && c.GetProperty("status").GetString() == "Disabled");
    }

    private async Task<JsonElement> LookupAsync(string username)
    {
        var response = await _client.GetAsync($"/api/v1/players/lookup?username={username}");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;
    }

    private async Task<JsonElement> RefreshAsync(Guid playerId)
    {
        var response = await _client.PostAsync($"/api/v1/players/{playerId}/refresh", content: null);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;
    }
}
