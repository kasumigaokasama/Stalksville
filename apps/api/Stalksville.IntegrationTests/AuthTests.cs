using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Stalksville.IntegrationTests;

public sealed class AuthTests : IAsyncLifetime
{
    private StalksvilleApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new StalksvilleApiFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAllAsync();

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsToken()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = _factory.AdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("token").GetString()));
        Assert.Equal("ADMIN", body.RootElement.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_IsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_RequireAuthentication()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/system/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedRequest_SucceedsWithToken()
    {
        var token = await LoginAsync(_factory);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await client.GetAsync("/api/v1/system/dashboard");
        var me = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = JsonDocument.Parse(await me.Content.ReadAsStreamAsync());
        Assert.Equal("admin", meBody.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Health_IsAnonymousAndReportsComponents()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        Assert.Equal("healthy", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("up", body.RootElement.GetProperty("checks").GetProperty("database").GetString());
        Assert.Equal("up", body.RootElement.GetProperty("checks").GetProperty("cache").GetString());
    }

    internal static async Task<string> LoginAsync(StalksvilleApiFactory factory)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = factory.AdminPassword });
        response.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("token").GetString()!;
    }
}
