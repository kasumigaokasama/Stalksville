using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>Phase 4+5: graph, paths, exposure, insights, compare, analytics.</summary>
public sealed class AdvancedIntelligenceTests : IAsyncLifetime
{
    private StalksvilleApiFactory _factory = null!;
    private string _token = null!;
    private HttpClient _client = null!;
    private HttpClient _mock = null!;
    private Guid _flexId;
    private Guid _talonId;

    public async Task InitializeAsync()
    {
        _factory = new StalksvilleApiFactory();
        _token = await AuthTests.LoginAsync(_factory);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", _token);
        _mock = _factory.Mock.CreateControlClient();

        // Import clan 2001: snapshots flex + talon, creates memberships and relationships.
        var import = await _client.PostAsync("/api/v1/clans/2001/import", content: null);
        import.EnsureSuccessStatusCode();

        var players = await _client.GetFromJsonAsync<JsonElement>("/api/v1/players");
        foreach (var player in players.EnumerateArray())
        {
            switch (player.GetProperty("username").GetString())
            {
                case "flex":
                    _flexId = player.GetProperty("id").GetGuid();
                    break;
                case "talon":
                    _talonId = player.GetProperty("id").GetGuid();
                    break;
            }
        }

        Assert.NotEqual(Guid.Empty, _flexId);
        Assert.NotEqual(Guid.Empty, _talonId);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _mock.Dispose();
        await _factory.DisposeAllAsync();
    }

    [Fact]
    public async Task Graph_ContainsPlayersClansAndConfidentEdges()
    {
        var graph = await _client.GetFromJsonAsync<JsonElement>("/api/v1/graph");

        var nodes = graph.GetProperty("nodes").EnumerateArray().ToList();
        Assert.Contains(nodes, n => n.GetProperty("type").GetString() == "clan" && n.GetProperty("label").GetString() == "Iron Fangs");
        Assert.Contains(nodes, n => n.GetProperty("type").GetString() == "player" && n.GetProperty("label").GetString() == "flex");

        var edges = graph.GetProperty("edges").EnumerateArray().ToList();
        var memberEdges = edges.Where(e => e.GetProperty("type").GetString() == "MEMBER_OF").ToList();
        Assert.NotEmpty(memberEdges);
        Assert.All(memberEdges, e => Assert.True(e.GetProperty("isCurrent").GetBoolean()));
        Assert.All(memberEdges, e => Assert.Equal(1.0, e.GetProperty("confidence").GetDouble()));
    }

    [Fact]
    public async Task GraphPaths_ConnectsClanmatesThroughTheClan()
    {
        var paths = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/graph/paths?from={_flexId}&to={_talonId}");

        var path = paths.GetProperty("paths").EnumerateArray().FirstOrDefault();
        Assert.NotEqual(default, path);
        var labels = path.EnumerateArray().Select(n => n.GetProperty("label").GetString()).ToList();
        Assert.Contains("flex", labels);
        Assert.Contains("Iron Fangs", labels);
        Assert.Contains("talon", labels);
        Assert.Equal("flex", labels.First());
        Assert.Equal("talon", labels.Last());
    }

    [Fact]
    public async Task GraphPaths_DisconnectedPlayers_ReturnEmpty()
    {
        // luna lives in clan 2002 and never interacts with 2001 members.
        var luna = await _client.GetFromJsonAsync<JsonElement>("/api/v1/players/lookup?username=luna");
        var lunaId = luna.GetProperty("dossier").GetProperty("player").GetProperty("id").GetGuid();

        var paths = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/graph/paths?from={_flexId}&to={lunaId}");

        Assert.Equal(0, paths.GetProperty("paths").GetArrayLength());
    }

    [Fact]
    public async Task Graph_CanBeScopedToAnInvestigation()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/investigations", new { title = "Iron Fangs network" });
        created.EnsureSuccessStatusCode();
        var workspace = JsonDocument.Parse(await created.Content.ReadAsStreamAsync()).RootElement;
        var caseId = workspace.GetProperty("investigation").GetProperty("id").GetGuid();

        var addTarget = await _client.PostAsJsonAsync($"/api/v1/investigations/{caseId}/targets", new
        {
            entityType = "player",
            entityId = _flexId,
        });
        addTarget.EnsureSuccessStatusCode();

        var graph = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/graph?investigationId={caseId}");
        var labels = graph.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("label").GetString()).ToList();

        // Ego network from flex: the target itself, its clan, and co-members.
        Assert.Contains("flex", labels);
        Assert.Contains("Iron Fangs", labels);
        Assert.Contains("talon", labels);
        Assert.DoesNotContain("luna", labels);
    }

    [Fact]
    public async Task Exposure_IsExplainable_AndBounded()
    {
        var exposure = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/players/{_flexId}/exposure");

        Assert.InRange(exposure.GetProperty("overall").GetInt32(), 0, 100);

        var categories = exposure.GetProperty("categories").EnumerateArray().ToList();
        Assert.Equal(5, categories.Count);
        Assert.All(categories, c => Assert.InRange(c.GetProperty("score").GetInt32(), 0, 100));
        Assert.All(categories, c => Assert.NotEmpty(c.GetProperty("factors").EnumerateArray()));
        Assert.All(
            categories.SelectMany(c => c.GetProperty("factors").EnumerateArray()),
            f => Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("evidence").GetString())));
    }

    [Fact]
    public async Task Insights_DetectMembershipVolatility_AfterRapidClanSwitches()
    {
        // Two quick clan switches for flex → membership volatility insight with evidence.
        var first = await _mock.PutAsJsonAsync("/_test/players/flex", new { clanId = "2002" });
        first.EnsureSuccessStatusCode();
        var refresh1 = await _client.PostAsync($"/api/v1/players/{_flexId}/refresh", content: null);
        refresh1.EnsureSuccessStatusCode();

        var second = await _mock.PutAsJsonAsync("/_test/players/flex", new { clanId = "2001" });
        second.EnsureSuccessStatusCode();
        var refresh2 = await _client.PostAsync($"/api/v1/players/{_flexId}/refresh", content: null);
        refresh2.EnsureSuccessStatusCode();

        var insights = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/players/{_flexId}/insights");

        var volatility = insights.EnumerateArray()
            .SingleOrDefault(i => i.GetProperty("classification").GetString() == "Membership volatility");

        Assert.NotEqual(default, volatility);
        Assert.True(volatility.GetProperty("confidence").GetDouble() >= 0.7);
        Assert.NotEmpty(volatility.GetProperty("evidenceChangeIds").EnumerateArray());
    }

    [Fact]
    public async Task Compare_ShowsFieldsAndSharedClanOverlap()
    {
        var compare = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/players/compare?a={_flexId}&b={_talonId}");

        Assert.Equal("flex", compare.GetProperty("a").GetProperty("player").GetProperty("username").GetString());
        Assert.Equal("talon", compare.GetProperty("b").GetProperty("player").GetProperty("username").GetString());

        var fields = compare.GetProperty("fields").EnumerateArray().ToList();
        Assert.Contains(fields, f => f.GetProperty("field").GetString() == "Level");

        var overlaps = compare.GetProperty("overlaps").EnumerateArray().ToList();
        var sharedClan = overlaps.SingleOrDefault(o => o.GetProperty("kind").GetString() == "Shared current clan");
        Assert.NotEqual(default, sharedClan);
        Assert.Equal(1.0, sharedClan.GetProperty("confidence").GetDouble());
        Assert.False(string.IsNullOrWhiteSpace(sharedClan.GetProperty("evidence").GetString()));

        Assert.Contains("Correlation is not proof", compare.GetProperty("disclaimer").GetString());
    }

    [Fact]
    public async Task Compare_SamePlayerTwice_IsRejected()
    {
        var response = await _client.GetAsync($"/api/v1/players/compare?a={_flexId}&b={_flexId}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analytics_SummaryHasTotalsAndSeries()
    {
        var response = await _client.GetAsync("/api/v1/analytics/summary?days=30");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var summary = JsonDocument.Parse(body).RootElement;

        Assert.True(summary.GetProperty("playersTracked").GetInt32() >= 2);
        Assert.True(summary.GetProperty("snapshotsCollected").GetInt32() >= 2);
        Assert.True(summary.GetProperty("changesDetected").GetInt32() >= 0);

        var series = summary.GetProperty("series").EnumerateArray().ToList();
        Assert.Equal(4, series.Count);
        Assert.Contains(series, s => s.GetProperty("name").GetString() == "Changes detected");
        // Today's import activity must show up as points.
        Assert.Contains(series, s => s.GetProperty("points").EnumerateArray().Any(p => p.GetProperty("value").GetInt32() > 0));
    }
}
