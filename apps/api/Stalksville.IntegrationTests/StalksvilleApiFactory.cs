using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Stalksville.Api;

namespace Stalksville.IntegrationTests;

/// <summary>
/// Boots the real Stalksville API in-process against a throwaway Postgres database and the
/// MockWolvesvilleServer. <see cref="MockWolvesvilleServer"/> runs on real localhost HTTP, so the
/// API's outbound client exercises the full handler pipeline (host guard, logging, resilience).
/// </summary>
public sealed class StalksvilleApiFactory : WebApplicationFactory<Program>
{
    public MockWolvesvilleServer Mock { get; } = new();

    public string ConnectionString { get; private set; } = null!;

    public string AdminPassword { get; } = $"test-admin-{Guid.NewGuid():N}";

    public bool AllowTestHost { get; init; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ConnectionString = TestDatabase.CreateAsync().GetAwaiter().GetResult();

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = ConnectionString,
            ["Wolvesville:Mode"] = "Real",
            ["Wolvesville:BaseUrl"] = Mock.BaseUrl,
            ["Wolvesville:ApiKey"] = MockWolvesvilleServer.ApiKey,
            ["Wolvesville:AllowTestHost"] = AllowTestHost ? "true" : "false",
            ["Wolvesville:MaxRequestsPerSecond"] = "1000",
            ["Auth:AdminPassword"] = AdminPassword,
            ["Auth:JwtKey"] = $"integration-test-signing-key-{Guid.NewGuid():N}-with-sufficient-length"
        }));
    }

    public async ValueTask DisposeAllAsync()
    {
        await DisposeAsync();
        await Mock.DisposeAsync();
        if (ConnectionString is not null)
        {
            await TestDatabase.DropAsync(ConnectionString);
        }
    }
}
