using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>Workspace search and investigation collaboration (assignment, tags, PATCH).</summary>
public sealed class SearchAndCollaborationTests : IAsyncLifetime
{
    private StalksvilleApiFactory _factory = null!;
    private string _token = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new StalksvilleApiFactory();
        _token = await AuthTests.LoginAsync(_factory);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", _token);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAllAsync();
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;
    }

    [Fact]
    public async Task Search_FindsPlayersClansAndCaseProse()
    {
        // Import a player + clan (the import fills in the clan name), create a case with prose, add a note.
        await _client.GetAsync("/api/v1/players/lookup?username=flex");
        await _client.PostAsync("/api/v1/clans/2001/import", content: null);
        var created = await _client.PostAsJsonAsync("/api/v1/investigations",
            new { title = "Silvermoon churn watch", description = "Tracking repeat clan hoppers in the silvermoon region." });
        created.EnsureSuccessStatusCode();
        var investigationId = JsonDocument.Parse(await created.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("investigation").GetProperty("id").GetGuid();
        await _client.PostAsJsonAsync($"/api/v1/investigations/{investigationId}/notes",
            new { content = "Hypothesis: the zephyrwind account is shared between two people." });

        var hits = await GetJsonAsync("/api/v1/search?q=flex");
        Assert.Contains(hits.EnumerateArray(), h => h.GetProperty("type").GetString() == "player" && h.GetProperty("title").GetString() == "flex");

        var clanHits = await GetJsonAsync("/api/v1/search?q=iron");
        Assert.Contains(clanHits.EnumerateArray(), h => h.GetProperty("type").GetString() == "clan");

        // FTS over the case title/description.
        var caseHits = await GetJsonAsync("/api/v1/search?q=churn");
        Assert.Contains(caseHits.EnumerateArray(),
            h => h.GetProperty("type").GetString() == "investigation" && h.GetProperty("title").GetString()!.Contains("Silvermoon"));

        // FTS over notes joins back to the case.
        var noteHits = await GetJsonAsync("/api/v1/search?q=zephyrwind");
        Assert.Contains(noteHits.EnumerateArray(), h => h.GetProperty("subtitle").GetString()!.Contains("zephyrwind"));

        // Short queries return empty, not error.
        var shortQuery = await GetJsonAsync("/api/v1/search?q=f");
        Assert.Empty(shortQuery.EnumerateArray());
    }

    [Fact]
    public async Task Search_EscapesLikeMetacharacters()
    {
        var response = await _client.GetAsync("/api/v1/search?q=%25%25"); // '%%'
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var underscore = await _client.GetAsync("/api/v1/search?q=___");
        Assert.Equal(HttpStatusCode.OK, underscore.StatusCode);
    }

    [Fact]
    public async Task Patch_UpdatesAssigneeAndTags()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/investigations", new { title = "Patchable case" });
        created.EnsureSuccessStatusCode();
        var id = JsonDocument.Parse(await created.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("investigation").GetProperty("id").GetGuid();

        // Assignees list contains the seeded admin.
        var assignees = await GetJsonAsync("/api/v1/investigations/assignees");
        var admin = assignees.EnumerateArray().Single(a => a.GetProperty("username").GetString() == "admin");
        var adminId = admin.GetProperty("id").GetGuid();

        var patched = await _client.PatchAsJsonAsync($"/api/v1/investigations/{id}",
            new { assignedToUserId = adminId, assigneeProvided = true, tags = new[] { "Churn", "silver-moon ", "churn", "" } });
        patched.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await patched.Content.ReadAsStreamAsync()).RootElement;

        Assert.Equal(adminId, body.GetProperty("investigation").GetProperty("assignedToUserId").GetGuid());
        // Tags normalize: trimmed, lowercased, deduped, empties dropped.
        Assert.Equal(["churn", "silver-moon"],
            body.GetProperty("investigation").GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ToArray());

        // Unassign with an explicit null.
        var cleared = await _client.PatchAsJsonAsync($"/api/v1/investigations/{id}",
            new { assignedToUserId = (Guid?)null, assigneeProvided = true });
        cleared.EnsureSuccessStatusCode();
        Assert.Null(JsonDocument.Parse(await cleared.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("investigation").GetProperty("assignedToUserId").GetString());

        // Title validation still applies through the patch path.
        var invalid = await _client.PatchAsJsonAsync($"/api/v1/investigations/{id}", new { title = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task HtmlExport_RendersPrintReport()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/investigations", new { title = "Printable case" });
        created.EnsureSuccessStatusCode();
        var id = JsonDocument.Parse(await created.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("investigation").GetProperty("id").GetGuid();

        var response = await _client.GetAsync($"/api/v1/investigations/{id}/export?format=html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<!doctype html>", html);
        Assert.Contains("Stalksville Investigation Report", html);
        Assert.Contains("window.print", html);
    }

    [Fact]
    public async Task Archive_RBAC_ViewerForbidden_AnalystAllowed()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/investigations", new { title = "RBAC archive case" });
        created.EnsureSuccessStatusCode();
        var id = JsonDocument.Parse(await created.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("investigation").GetProperty("id").GetGuid();

        var viewerUsername = $"viewer-{Guid.NewGuid():N}"[..32];
        var viewerPassword = $"viewer-{Guid.NewGuid():N}";
        var userCreated = await _client.PostAsJsonAsync("/api/v1/admin/users",
            new { username = viewerUsername, password = viewerPassword, role = "VIEWER" });
        userCreated.EnsureSuccessStatusCode();
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { username = viewerUsername, password = viewerPassword });
        login.EnsureSuccessStatusCode();
        var viewerToken = JsonDocument.Parse(await login.Content.ReadAsStreamAsync()).RootElement.GetProperty("token").GetString();

        using var viewerClient = _factory.CreateClient();
        viewerClient.DefaultRequestHeaders.Authorization = new("Bearer", viewerToken);
        var viewerArchive = await viewerClient.PostAsync($"/api/v1/investigations/{id}/archive", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, viewerArchive.StatusCode);
        var viewerReopen = await viewerClient.PostAsync($"/api/v1/investigations/{id}/reopen", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, viewerReopen.StatusCode);

        // Analysts (and admins) may still change case status.
        var archived = await _client.PostAsync($"/api/v1/investigations/{id}/archive", content: null);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        var reopened = await _client.PostAsync($"/api/v1/investigations/{id}/reopen", content: null);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
    }
}
