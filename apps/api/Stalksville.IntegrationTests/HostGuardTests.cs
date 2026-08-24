using System.Net;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>
/// Verifies the outbound HostGuard: without the explicit test-host allowance, requests to a
/// loopback upstream are rejected before leaving the process — even though the base URL points
/// at the (local) mock server.
/// </summary>
public sealed class HostGuardTests : IAsyncLifetime
{
    private StalksvilleApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new StalksvilleApiFactory { AllowTestHost = false };
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAllAsync();

    [Fact]
    public async Task LoopbackUpstream_IsBlockedWithoutTestHostAllowance()
    {
        var token = await AuthTests.LoginAsync(_factory);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await client.GetAsync("/api/v1/players/lookup?username=flex");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("security policy", body, StringComparison.OrdinalIgnoreCase);
    }
}
