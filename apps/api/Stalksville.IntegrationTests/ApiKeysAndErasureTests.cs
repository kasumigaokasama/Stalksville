using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>Client API keys (X-Api-Key) and admin player erasure.</summary>
public sealed class ApiKeysAndErasureTests : IAsyncLifetime
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
    public async Task ApiKey_AuthenticatesWithUserRoleAndRevocationKillsIt()
    {
        var adminId = (await GetJsonAsync("/api/v1/auth/me")).GetProperty("id").GetGuid();

        var created = await _client.PostAsJsonAsync($"/api/v1/admin/users/{adminId}/api-keys", new { name = "test-exporter" });
        created.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await created.Content.ReadAsStreamAsync()).RootElement;
        var key = body.GetProperty("apiKey").GetString()!;
        var keyId = body.GetProperty("key").GetProperty("id").GetGuid();

        Assert.StartsWith("stv_", key);
        Assert.Equal(12, body.GetProperty("key").GetProperty("prefix").GetString()!.Length);

        // The key authenticates with the same claims as a JWT (name carries through).
        using var keyClient = _factory.CreateClient();
        keyClient.DefaultRequestHeaders.Add("X-Api-Key", key);
        var me = await keyClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal("admin", JsonDocument.Parse(await me.Content.ReadAsStreamAsync()).RootElement.GetProperty("username").GetString());

        // LastUsed is stamped.
        var listed = await GetJsonAsync($"/api/v1/admin/users/{adminId}/api-keys");
        var entry = listed.EnumerateArray().Single(k => k.GetProperty("id").GetGuid() == keyId);
        Assert.NotNull(entry.GetProperty("lastUsedAt").GetString());

        // Revocation takes effect immediately.
        var revoked = await _client.DeleteAsync($"/api/v1/admin/users/{adminId}/api-keys/{keyId}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        var afterRevoke = await keyClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task Erase_RemovesPlayerAndAllDependents()
    {
        var lookup = await _client.GetAsync("/api/v1/players/lookup?username=flex");
        lookup.EnsureSuccessStatusCode();
        var playerId = JsonDocument.Parse(await lookup.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("dossier").GetProperty("player").GetProperty("id").GetGuid();

        // Generate some dependent data: a change (level jump) + alert + timeline events exist after a mutation.
        var mutate = await _factory.Mock.CreateControlClient()
            .PutAsJsonAsync("/_test/players/flex", new { level = 62 });
        mutate.EnsureSuccessStatusCode();
        var refresh = await _client.PostAsync($"/api/v1/players/{playerId}/refresh", content: null);
        refresh.EnsureSuccessStatusCode();

        // Erasure requires a reason.
        var noReason = await _client.DeleteAsync($"/api/v1/players/{playerId}");
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var erased = await _client.DeleteAsync($"/api/v1/players/{playerId}?reason=data-protection request");
        Assert.Equal(HttpStatusCode.NoContent, erased.StatusCode);

        var missing = await _client.GetAsync($"/api/v1/players/{playerId}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // Timeline and alerts no longer reference the erased player.
        var timeline = await GetJsonAsync($"/api/v1/timeline?entityId={playerId}");
        Assert.Empty(timeline.EnumerateArray());
        var alerts = await GetJsonAsync($"/api/v1/alerts?entityId={playerId}");
        Assert.Empty(alerts.EnumerateArray());

        // The tracked list no longer contains the player.
        var players = await GetJsonAsync("/api/v1/players");
        Assert.DoesNotContain(players.EnumerateArray(), p => p.GetProperty("id").GetGuid() == playerId);
    }

    [Fact]
    public async Task Erase_RequiresAdminRole()
    {
        // Throwaway viewer with per-run credentials — nothing static in source.
        var viewerUsername = $"spectator{Guid.NewGuid():N}"[..20];
        var viewerPassword = $"viewer-{Guid.NewGuid():N}";
        var created = await _client.PostAsJsonAsync("/api/v1/admin/users", new { username = viewerUsername, password = viewerPassword, role = "VIEWER" });
        Assert.True(created.IsSuccessStatusCode || created.StatusCode == HttpStatusCode.Conflict);

        var login = await _client.PostAsync("/api/v1/auth/login", JsonContent.Create(new { username = viewerUsername, password = viewerPassword }));
        login.EnsureSuccessStatusCode();
        var viewerToken = JsonDocument.Parse(await login.Content.ReadAsStreamAsync()).RootElement.GetProperty("token").GetString();

        using var viewerClient = _factory.CreateClient();
        viewerClient.DefaultRequestHeaders.Authorization = new("Bearer", viewerToken);
        var response = await viewerClient.DeleteAsync($"/api/v1/players/{Guid.NewGuid()}?reason=test");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
