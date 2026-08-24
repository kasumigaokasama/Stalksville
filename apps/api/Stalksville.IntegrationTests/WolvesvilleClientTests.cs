using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>Outbound client behavior: resilience against upstream failures, host guarding.</summary>
public sealed class WolvesvilleClientTests : IAsyncLifetime
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
    public async Task Transient429s_AreRetriedAndEventuallySucceed()
    {
        // Two scripted 429s, then the mock serves normally; the pipeline retries with backoff.
        var script = await _mockControl.PostAsJsonAsync("/_test/failures", new { status = 429, count = 2 });
        script.EnsureSuccessStatusCode();

        var response = await _client.GetAsync("/api/v1/players/lookup?username=flex");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        Assert.Equal("flex", body.RootElement.GetProperty("dossier").GetProperty("player").GetProperty("username").GetString());
    }

    [Fact]
    public async Task Repeated500s_SurfaceAsUpstreamError()
    {
        var script = await _mockControl.PostAsJsonAsync("/_test/failures", new { status = 500, count = 5 });
        script.EnsureSuccessStatusCode();

        var response = await _client.GetAsync("/api/v1/players/lookup?username=flex");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task MissingWolvesvilleAuthHeadersOnClient_WouldBeRejectedByUpstream()
    {
        // The mock enforces the same header rules as the real API (Bot auth + Accept json).
        // If the Stalksville client ever stopped sending them, every lookup would 401 → 502.
        var response = await _client.GetAsync("/api/v1/players/lookup?username=flex");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
