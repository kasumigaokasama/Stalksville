using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>Phase 6+7: AI explain, exports, RBAC and user management.</summary>
public sealed class OperationsTests : IAsyncLifetime
{
    private StalksvilleApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _playerId;
    private Guid _caseId;

    public async Task InitializeAsync()
    {
        _factory = new StalksvilleApiFactory();
        _client = _factory.CreateClient();

        // Login as admin.
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = _factory.AdminPassword,
        });
        var token = JsonDocument.Parse(await login.Content.ReadAsStreamAsync()).RootElement.GetProperty("token").GetString();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Import a player and open a case so there is something to explain and export.
        var lookup = await _client.GetFromJsonAsync<JsonElement>("/api/v1/players/lookup?username=flex");
        _playerId = lookup.GetProperty("dossier").GetProperty("player").GetProperty("id").GetGuid();

        var created = await _client.PostAsJsonAsync("/api/v1/investigations", new { title = "Explain and export me" });
        var workspace = JsonDocument.Parse(await created.Content.ReadAsStreamAsync()).RootElement;
        _caseId = workspace.GetProperty("investigation").GetProperty("id").GetGuid();

        var addTarget = await _client.PostAsJsonAsync($"/api/v1/investigations/{_caseId}/targets", new
        {
            entityType = "player",
            entityId = _playerId,
        });
        addTarget.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAllAsync();
    }

    [Fact]
    public async Task Explain_ReturnsGuardrailSections_FromDeterministicNarrator()
    {
        var response = await _client.PostAsync($"/api/v1/investigations/{_caseId}/explain", content: null);
        response.EnsureSuccessStatusCode();

        var narrative = JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;

        Assert.Equal("deterministic", narrative.GetProperty("provider").GetString());
        Assert.NotEmpty(narrative.GetProperty("observed").EnumerateArray());
        Assert.NotEmpty(narrative.GetProperty("derived").EnumerateArray());
        Assert.NotEmpty(narrative.GetProperty("hypothesis").EnumerateArray());
        Assert.NotEmpty(narrative.GetProperty("unknown").EnumerateArray());
        Assert.Contains("real-world person", narrative.GetProperty("unknown").EnumerateArray().First().GetString());
        Assert.Contains("Hypotheses are unproven", narrative.GetProperty("disclaimer").GetString());

        // Observed section must reference facts from the workspace only.
        Assert.Contains(narrative.GetProperty("observed").EnumerateArray(),
            o => o.GetString()!.Contains("flex"));
    }

    [Fact]
    public async Task Export_MarkdownContainsReportSections()
    {
        var response = await _client.GetAsync($"/api/v1/investigations/{_caseId}/export?format=md");
        response.EnsureSuccessStatusCode();

        Assert.Equal("text/markdown; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.ToString() ?? "");

        var markdown = await response.Content.ReadAsStringAsync();
        Assert.Contains("# Stalksville Investigation Report", markdown);
        Assert.Contains("Explain and export me", markdown);
        Assert.Contains("## Targets", markdown);
        Assert.Contains("flex", markdown);
        Assert.Contains("## Timeline", markdown);
        Assert.Contains("## Confidence & limitations", markdown);
        Assert.Contains("Hypotheses are unproven", markdown);
    }

    [Fact]
    public async Task Export_CsvHasHeaderAndRows()
    {
        var response = await _client.GetAsync($"/api/v1/investigations/{_caseId}/export?format=csv");
        response.EnsureSuccessStatusCode();

        var csv = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("occurred_at,event_type,entity_type,entity_id,observed_or_derived,summary", csv);
        Assert.Contains("PlayerDiscovered", csv);
    }

    [Fact]
    public async Task Export_JsonParsesAsWorkspace()
    {
        var response = await _client.GetAsync($"/api/v1/investigations/{_caseId}/export?format=json");
        response.EnsureSuccessStatusCode();

        var workspace = JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;
        Assert.Equal("Explain and export me", workspace.GetProperty("investigation").GetProperty("title").GetString());
        Assert.Equal(1, workspace.GetProperty("targets").GetArrayLength());
    }

    [Fact]
    public async Task Export_RejectsUnknownFormat()
    {
        var response = await _client.GetAsync($"/api/v1/investigations/{_caseId}/export?format=exe");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateViewer_WhoIsReadOnly()
    {
        var password = $"viewer-{Guid.NewGuid():N}";
        var created = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            username = "spectator",
            password,
            role = "VIEWER",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var users = await _client.GetFromJsonAsync<JsonElement>("/api/v1/admin/users");
        Assert.Contains(users.EnumerateArray(), u => u.GetProperty("username").GetString() == "spectator");

        // Duplicate username is a conflict.
        var duplicate = await _client.PostAsJsonAsync("/api/v1/admin/users", new { username = "spectator", password, role = "VIEWER" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // The viewer can read but not mutate.
        using var viewer = _factory.CreateClient();
        var viewerLogin = await viewer.PostAsJsonAsync("/api/v1/auth/login", new { username = "spectator", password });
        viewerLogin.EnsureSuccessStatusCode();
        var viewerToken = JsonDocument.Parse(await viewerLogin.Content.ReadAsStreamAsync()).RootElement.GetProperty("token").GetString();
        viewer.DefaultRequestHeaders.Authorization = new("Bearer", viewerToken);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/investigations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/v1/investigations", new { title = "nope nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/v1/players/{_playerId}/refresh", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/players/lookup?username=flex")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Viewer_CanStillExplainAndExport()
    {
        var password = $"viewer-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/admin/users", new { username = "reader", password, role = "VIEWER" });

        using var viewer = _factory.CreateClient();
        var login = await viewer.PostAsJsonAsync("/api/v1/auth/login", new { username = "reader", password });
        var token = JsonDocument.Parse(await login.Content.ReadAsStreamAsync()).RootElement.GetProperty("token").GetString();
        viewer.DefaultRequestHeaders.Authorization = new("Bearer", token);

        Assert.Equal(HttpStatusCode.OK, (await viewer.PostAsync($"/api/v1/investigations/{_caseId}/explain", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/v1/investigations/{_caseId}/export?format=md")).StatusCode);
    }

    [Fact]
    public async Task UserCreation_ValidatesInput()
    {
        var badRole = await _client.PostAsJsonAsync("/api/v1/admin/users", new { username = "someone", password = "longenough1", role = "EMPEROR" });
        Assert.Equal(HttpStatusCode.BadRequest, badRole.StatusCode);

        var shortPassword = await _client.PostAsJsonAsync("/api/v1/admin/users", new { username = "someone", password = "short", role = "ANALYST" });
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);
    }
}
