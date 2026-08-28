using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>Derived alert pipeline: change detection → alert with evidence → inbox read state.</summary>
public sealed class AlertTests : IAsyncLifetime
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
    public async Task Refresh_AfterLevelJump_CreatesEvidenceBackedAlert()
    {
        var playerId = await ImportFlexAsync();

        var mutate = await _mockControl.PutAsJsonAsync("/_test/players/flex", new { level = 62 });
        mutate.EnsureSuccessStatusCode();
        await RefreshAsync(playerId);

        var alerts = await GetJsonAsync("/api/v1/alerts?kind=LevelJump");
        var alert = Assert.Single(alerts.EnumerateArray());

        Assert.Equal("flex", alert.GetProperty("entityTitle").GetString());
        Assert.Equal("notice", alert.GetProperty("severity").GetString());
        Assert.Equal(playerId, alert.GetProperty("entityId").GetGuid());

        // Evidence must point at the backing change and its snapshots.
        var evidence = alert.GetProperty("evidence");
        Assert.Equal(playerId, evidence.GetProperty("playerId").GetGuid());
        var change = Assert.Single(evidence.GetProperty("changes").EnumerateArray());
        Assert.Equal("level", change.GetProperty("field").GetString());
        Assert.Equal("42", change.GetProperty("oldValue").GetString());
        Assert.Equal("62", change.GetProperty("newValue").GetString());

        // The referenced change id really exists in the player's change history.
        var changeId = change.GetProperty("id").GetGuid();
        var changes = await GetJsonAsync($"/api/v1/players/{playerId}/changes");
        Assert.Contains(changes.EnumerateArray(), c => c.GetProperty("id").GetGuid() == changeId);

        var unread = await GetJsonAsync("/api/v1/alerts/unread-count");
        Assert.Equal(1, unread.GetProperty("unread").GetInt32());
    }

    [Fact]
    public async Task Oscillation_CreatesOneAlertPerRealChange()
    {
        var playerId = await ImportFlexAsync();

        var first = await _mockControl.PutAsJsonAsync("/_test/players/flex", new { clanId = "2002" });
        first.EnsureSuccessStatusCode();
        await RefreshAsync(playerId);

        var back = await _mockControl.PutAsJsonAsync("/_test/players/flex", new { clanId = "2001" });
        back.EnsureSuccessStatusCode();
        await RefreshAsync(playerId);

        // A→B→A within a day is two real transitions — both alert, each backed by its own change.
        var alerts = await GetJsonAsync("/api/v1/alerts?kind=ClanChanged");
        var list = alerts.EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.Contains(list, a => a.GetProperty("body").GetString()!.Contains("2001 → 2002"));
        Assert.Contains(list, a => a.GetProperty("body").GetString()!.Contains("2002 → 2001"));
    }

    [Fact]
    public async Task RefreshingUnchangedState_DoesNotDuplicateAlerts()
    {
        var playerId = await ImportFlexAsync();

        var mutate = await _mockControl.PutAsJsonAsync("/_test/players/flex", new { clanId = "2002" });
        mutate.EnsureSuccessStatusCode();
        await RefreshAsync(playerId);

        // Re-refresh: state unchanged → smart snapshot → no changes → no duplicate alert.
        await RefreshAsync(playerId);

        var alerts = await GetJsonAsync("/api/v1/alerts?kind=ClanChanged");
        Assert.Single(alerts.EnumerateArray());
    }

    [Fact]
    public async Task OrdinaryDrift_CreatesNoAlerts()
    {
        var playerId = await ImportFlexAsync();

        var mutate = await _mockControl.PutAsJsonAsync("/_test/players/flex", new { wins = 420 });
        mutate.EnsureSuccessStatusCode();
        await RefreshAsync(playerId);

        var alerts = await GetJsonAsync("/api/v1/alerts");
        Assert.Empty(alerts.EnumerateArray());
        var unread = await GetJsonAsync("/api/v1/alerts/unread-count");
        Assert.Equal(0, unread.GetProperty("unread").GetInt32());
    }

    [Fact]
    public async Task MarkRead_AndReadAll_ClearUnreadCount()
    {
        var playerId = await ImportFlexAsync();

        var mutate = await _mockControl.PutAsJsonAsync("/_test/players/flex", new { clanId = "2002" });
        mutate.EnsureSuccessStatusCode();
        await RefreshAsync(playerId);

        var alerts = await GetJsonAsync("/api/v1/alerts");
        var alert = Assert.Single(alerts.EnumerateArray());
        Assert.Null(alert.GetProperty("readAt").GetString());

        var read = await _client.PostAsync($"/api/v1/alerts/{alert.GetProperty("id").GetGuid()}/read", content: null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);

        // Re-reading an already-read alert is a 404 (nothing left to transition).
        var again = await _client.PostAsync($"/api/v1/alerts/{alert.GetProperty("id").GetGuid()}/read", content: null);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);

        var unread = await GetJsonAsync("/api/v1/alerts/unread-count");
        Assert.Equal(0, unread.GetProperty("unread").GetInt32());
        var listed = await GetJsonAsync("/api/v1/alerts?unreadOnly=true");
        Assert.Empty(listed.EnumerateArray());

        // read-all is idempotent and returns no content.
        var readAll = await _client.PostAsync("/api/v1/alerts/read-all", content: null);
        Assert.Equal(HttpStatusCode.NoContent, readAll.StatusCode);
    }

    [Fact]
    public async Task AlertFilters_ByEntityAndUnreadOnly()
    {
        var flexId = await ImportFlexAsync();
        var talonId = await ImportTalonAsync();

        var mutate = await _mockControl.PutAsJsonAsync("/_test/players/flex", new { clanId = "2002" });
        mutate.EnsureSuccessStatusCode();
        await RefreshAsync(flexId);

        // The clan move raises both a ClanChanged alert and (default threshold 15) an ExposureShift.
        var forFlex = await GetJsonAsync($"/api/v1/alerts?entityId={flexId}&kind=ClanChanged");
        Assert.Single(forFlex.EnumerateArray());

        var shifts = await GetJsonAsync($"/api/v1/alerts?entityId={flexId}&kind=ExposureShift");
        Assert.Contains(shifts.EnumerateArray(), a => a.GetProperty("title").GetString()!.Contains("exposure"));

        var forTalon = await GetJsonAsync($"/api/v1/alerts?entityId={talonId}");
        Assert.Empty(forTalon.EnumerateArray());
    }

    private async Task<Guid> ImportFlexAsync()
    {
        var lookup = await GetJsonAsync("/api/v1/players/lookup?username=flex");
        return lookup.GetProperty("dossier").GetProperty("player").GetProperty("id").GetGuid();
    }

    private async Task<Guid> ImportTalonAsync()
    {
        var lookup = await GetJsonAsync("/api/v1/players/lookup?username=talon");
        return lookup.GetProperty("dossier").GetProperty("player").GetProperty("id").GetGuid();
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

    [Fact]
    public async Task ReadState_IsPerUser()
    {
        // Generate at least one alert.
        var flexId = await ImportFlexAsync();
        var mutate = await _mockControl.PutAsJsonAsync("/_test/players/flex", new { clanId = "2002" });
        mutate.EnsureSuccessStatusCode();
        await RefreshAsync(flexId);

        // Admin reads everything.
        var readAll = await _client.PostAsync("/api/v1/alerts/read-all", content: null);
        readAll.EnsureSuccessStatusCode();
        Assert.Equal(0, (await GetJsonAsync("/api/v1/alerts/unread-count")).GetProperty("unread").GetInt32());

        // A second account still sees those alerts as unread.
        var viewerUsername = $"viewer-{Guid.NewGuid():N}"[..32];
        var viewerPassword = $"viewer-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/admin/users",
            new { username = viewerUsername, password = viewerPassword, role = "VIEWER" });
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { username = viewerUsername, password = viewerPassword });
        login.EnsureSuccessStatusCode();
        var viewerToken = JsonDocument.Parse(await login.Content.ReadAsStreamAsync()).RootElement.GetProperty("token").GetString();

        using var viewerClient = _factory.CreateClient();
        viewerClient.DefaultRequestHeaders.Authorization = new("Bearer", viewerToken);

        var viewerUnread = JsonDocument.Parse(
            await (await viewerClient.GetAsync("/api/v1/alerts/unread-count")).Content.ReadAsStreamAsync()).RootElement;
        Assert.True(viewerUnread.GetProperty("unread").GetInt32() > 0);

        // The viewer reads one alert; the admin's zero-unread state is unaffected.
        var unreadList = JsonDocument.Parse(
            await (await viewerClient.GetAsync("/api/v1/alerts?unreadOnly=true")).Content.ReadAsStreamAsync()).RootElement;
        var firstAlert = unreadList.EnumerateArray().First();
        Assert.Null(firstAlert.GetProperty("readAt").GetString());

        var markOne = await viewerClient.PostAsync($"/api/v1/alerts/{firstAlert.GetProperty("id").GetString()}/read", content: null);
        Assert.Equal(HttpStatusCode.NoContent, markOne.StatusCode);

        Assert.Equal(0, (await GetJsonAsync("/api/v1/alerts/unread-count")).GetProperty("unread").GetInt32());
    }
}
