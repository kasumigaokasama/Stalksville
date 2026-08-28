using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>Per-user watchlists: star/unstar from any role, list for the caller, worker priority ids.</summary>
public sealed class WatchlistTests : IAsyncLifetime
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
    public async Task Star_List_Unstar_RoundTripsForTheCallingUser()
    {
        var lookup = await _client.GetAsync("/api/v1/players/lookup?username=flex");
        lookup.EnsureSuccessStatusCode();
        var flexId = JsonDocument.Parse(await lookup.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("dossier").GetProperty("player").GetProperty("id").GetString();

        var star = await _client.PostAsync($"/api/v1/players/{flexId}/watch", content: null);
        Assert.Equal(HttpStatusCode.NoContent, star.StatusCode);

        var list = await GetJsonAsync("/api/v1/watchlist");
        var entry = Assert.Single(list.EnumerateArray());
        Assert.Equal("flex", entry.GetProperty("username").GetString());
        Assert.Equal(flexId, entry.GetProperty("id").GetString());

        var unstar = await _client.DeleteAsync($"/api/v1/players/{flexId}/watch");
        Assert.Equal(HttpStatusCode.NoContent, unstar.StatusCode);
        var after = await GetJsonAsync("/api/v1/watchlist");
        Assert.Empty(after.EnumerateArray());
    }

    [Fact]
    public async Task ViewersMayStar_AndStarsArePerUser()
    {
        var lookup = await _client.GetAsync("/api/v1/players/lookup?username=talon");
        lookup.EnsureSuccessStatusCode();
        var talonId = JsonDocument.Parse(await lookup.Content.ReadAsStreamAsync())
            .RootElement.GetProperty("dossier").GetProperty("player").GetProperty("id").GetString();

        var viewerUsername = $"viewer-{Guid.NewGuid():N}"[..32];
        var viewerPassword = $"viewer-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/admin/users",
            new { username = viewerUsername, password = viewerPassword, role = "VIEWER" });
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { username = viewerUsername, password = viewerPassword });
        login.EnsureSuccessStatusCode();
        var viewerToken = JsonDocument.Parse(await login.Content.ReadAsStreamAsync()).RootElement.GetProperty("token").GetString();

        using var viewerClient = _factory.CreateClient();
        viewerClient.DefaultRequestHeaders.Authorization = new("Bearer", viewerToken);

        // VIEWER is read-only for intelligence mutations, but starring is personal bookkeeping.
        var star = await viewerClient.PostAsync($"/api/v1/players/{talonId}/watch", content: null);
        Assert.Equal(HttpStatusCode.NoContent, star.StatusCode);

        var viewerList = JsonDocument.Parse(await (await viewerClient.GetAsync("/api/v1/watchlist")).Content.ReadAsStreamAsync()).RootElement;
        Assert.Single(viewerList.EnumerateArray(), e => e.GetProperty("username").GetString() == "talon");

        // The admin's watchlist is unaffected.
        var adminList = await GetJsonAsync("/api/v1/watchlist");
        Assert.DoesNotContain(adminList.EnumerateArray(), e => e.GetProperty("username").GetString() == "talon");
    }
}
