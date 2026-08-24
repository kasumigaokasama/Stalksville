using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>Investigation lifecycle: create → targets → notes → aggregated timeline/stats → archive.</summary>
public sealed class InvestigationTests : IAsyncLifetime
{
    private StalksvilleApiFactory _factory = null!;
    private string _token = null!;
    private HttpClient _client = null!;
    private Guid _playerId;

    public async Task InitializeAsync()
    {
        _factory = new StalksvilleApiFactory();
        _token = await AuthTests.LoginAsync(_factory);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", _token);

        // Import a player so the investigation has a real tracked entity to target.
        var lookup = await _client.GetFromJsonAsync<JsonElement>("/api/v1/players/lookup?username=flex");
        _playerId = lookup.GetProperty("dossier").GetProperty("player").GetProperty("id").GetGuid();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAllAsync();
    }

    private async Task<JsonElement> CreateCaseAsync(string title)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/investigations", new { title, description = (string?)null });
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;
    }

    [Fact]
    public async Task Create_ListsCaseWithCounts()
    {
        await CreateCaseAsync("Iron Fangs churn");

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/v1/investigations");
        var first = list.EnumerateArray().Single();
        Assert.Equal("Iron Fangs churn", first.GetProperty("title").GetString());
        Assert.Equal("active", first.GetProperty("status").GetString());
        Assert.True(first.GetProperty("caseNumber").GetInt32() > 0);
        Assert.Equal(0, first.GetProperty("targetCount").GetInt32());
    }

    [Fact]
    public async Task Create_RejectsShortTitles()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/investigations", new { title = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddTarget_AggregatesTimelineAndStats()
    {
        var created = await CreateCaseAsync("Network of flex");
        var caseId = created.GetProperty("investigation").GetProperty("id").GetGuid();

        var addTarget = await _client.PostAsJsonAsync($"/api/v1/investigations/{caseId}/targets", new
        {
            entityType = "player",
            entityId = _playerId,
        });
        addTarget.EnsureSuccessStatusCode();
        var workspace = JsonDocument.Parse(await addTarget.Content.ReadAsStreamAsync()).RootElement;

        // The target resolves to the tracked player with its observation history.
        var target = workspace.GetProperty("targets").EnumerateArray().Single();
        Assert.Equal("flex", target.GetProperty("displayName").GetString());
        Assert.Equal(1, target.GetProperty("snapshotCount").GetInt32());
        Assert.Equal(1, target.GetProperty("currentRelationships").GetInt32());

        // Timeline aggregates the target's events (discovery at minimum).
        var stats = workspace.GetProperty("stats");
        Assert.Equal(1, stats.GetProperty("targets").GetInt32());
        Assert.True(stats.GetProperty("timelineEvents").GetInt32() >= 1);
        Assert.True(stats.GetProperty("snapshotsCollected").GetInt32() >= 1);
        Assert.True(stats.GetProperty("highConfidenceRelationships").GetInt32() >= 1);

        var timeline = workspace.GetProperty("timeline").EnumerateArray().ToList();
        Assert.Contains(timeline, e => e.GetProperty("eventType").GetString() == "PlayerDiscovered");
    }

    [Fact]
    public async Task AddTarget_UnknownEntity_Returns404()
    {
        var created = await CreateCaseAsync("Ghost hunt");
        var caseId = created.GetProperty("investigation").GetProperty("id").GetGuid();

        var response = await _client.PostAsJsonAsync($"/api/v1/investigations/{caseId}/targets", new
        {
            entityType = "player",
            entityId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Notes_AreStoredWithAuthor()
    {
        var created = await CreateCaseAsync("Notebook case");
        var caseId = created.GetProperty("investigation").GetProperty("id").GetGuid();

        var addNote = await _client.PostAsJsonAsync($"/api/v1/investigations/{caseId}/notes", new { content = "Flex looks dormant." });
        addNote.EnsureSuccessStatusCode();

        var workspace = JsonDocument.Parse(await addNote.Content.ReadAsStreamAsync()).RootElement;
        var note = workspace.GetProperty("notes").EnumerateArray().Single();
        Assert.Equal("Flex looks dormant.", note.GetProperty("content").GetString());
        Assert.Equal("admin", note.GetProperty("author").GetString());
    }

    [Fact]
    public async Task ArchivedCase_RejectsModifications_AndHidesFromDefaultList()
    {
        var created = await CreateCaseAsync("Cold case");
        var caseId = created.GetProperty("investigation").GetProperty("id").GetGuid();

        var archive = await _client.PostAsync($"/api/v1/investigations/{caseId}/archive", content: null);
        archive.EnsureSuccessStatusCode();

        var addNote = await _client.PostAsJsonAsync($"/api/v1/investigations/{caseId}/notes", new { content = "should fail" });
        Assert.Equal(HttpStatusCode.Conflict, addNote.StatusCode);

        var activeList = await _client.GetFromJsonAsync<JsonElement>("/api/v1/investigations");
        Assert.DoesNotContain(activeList.EnumerateArray(), i => i.GetProperty("id").GetGuid() == caseId);

        var archivedList = await _client.GetFromJsonAsync<JsonElement>("/api/v1/investigations?includeArchived=true");
        Assert.Contains(archivedList.EnumerateArray(), i => i.GetProperty("id").GetGuid() == caseId);
    }

    [Fact]
    public async Task TimelineEndpoint_FiltersByDerivation()
    {
        var observed = await _client.GetFromJsonAsync<JsonElement>("/api/v1/timeline?derived=false&limit=50");
        Assert.All(observed.EnumerateArray(), e => Assert.False(e.GetProperty("isDerived").GetBoolean()));

        var all = await _client.GetFromJsonAsync<JsonElement>("/api/v1/timeline?limit=50");
        Assert.True(all.GetArrayLength() >= observed.GetArrayLength());
    }

    [Fact]
    public async Task TimelineEndpoint_RejectsUnknownEntityFilter()
    {
        var response = await _client.GetAsync("/api/v1/timeline?entity=banana");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
