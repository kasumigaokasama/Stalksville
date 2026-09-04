using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Xunit;

namespace Stalksville.IntegrationTests;

/// <summary>
/// Scheduled scans: schedule CRUD + validation, run-now through the real pipeline, and webhook
/// notifications that fire only when a run detects changes. Webhook delivery is verified against
/// a real local Kestrel receiver (the same trick MockWolvesvilleServer uses).
/// </summary>
public sealed class ScanTests : IAsyncLifetime
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
        await _factory.DisposeAllAsync();
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync()).RootElement;
    }

    [Fact]
    public async Task ScheduleCrud_RoundTrips_AndValidates()
    {
        var create = await _client.PostAsJsonAsync("/api/v1/scans/schedules",
            new { name = "Nightly sweep", kind = "player-refresh", intervalMinutes = 30, batchSize = 7 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = JsonDocument.Parse(await create.Content.ReadAsStreamAsync()).RootElement;
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(30, created.GetProperty("intervalMinutes").GetInt32());
        Assert.Equal(7, created.GetProperty("batchSize").GetInt32());
        Assert.True(created.GetProperty("enabled").GetBoolean());
        Assert.NotNull(created.GetProperty("nextRunAt")); // fresh schedules are due immediately

        var list = await GetJsonAsync("/api/v1/scans/schedules");
        Assert.Contains(list.EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);

        var pause = await _client.PatchAsJsonAsync($"/api/v1/scans/schedules/{id}",
            new { name = "Nightly sweep", enabled = false });
        Assert.Equal(HttpStatusCode.OK, pause.StatusCode);
        var paused = JsonDocument.Parse(await pause.Content.ReadAsStreamAsync()).RootElement;
        Assert.False(paused.GetProperty("enabled").GetBoolean());

        var delete = await _client.DeleteAsync($"/api/v1/scans/schedules/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var after = await GetJsonAsync("/api/v1/scans/schedules");
        Assert.DoesNotContain(after.EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);

        // Validation: interval below the floor and an unknown kind are rejected as 400s.
        var badInterval = await _client.PostAsJsonAsync("/api/v1/scans/schedules",
            new { name = "Too tight", kind = "player-refresh", intervalMinutes = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, badInterval.StatusCode);
        var badKind = await _client.PostAsJsonAsync("/api/v1/scans/schedules",
            new { name = "Weird kind", kind = "mind-read", intervalMinutes = 30 });
        Assert.Equal(HttpStatusCode.BadRequest, badKind.StatusCode);
    }

    [Fact]
    public async Task ChannelValidation_RejectsNonHttpsUrls()
    {
        var create = await _client.PostAsJsonAsync("/api/v1/scans/notifications/channels",
            new { name = "Insecure", targetUrl = "http://127.0.0.1:9/hook" });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        var ok = await _client.PostAsJsonAsync("/api/v1/scans/notifications/channels",
            new { name = "Ops Discord", targetUrl = "https://discord.com/api/webhooks/123456/abcdef" });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var channel = JsonDocument.Parse(await ok.Content.ReadAsStreamAsync()).RootElement;
        // The URL embeds a token — the response must mask it.
        Assert.DoesNotContain("abcdef", channel.GetProperty("targetUrlMasked").GetString());
    }

    [Fact]
    public async Task RunNow_RecordsRun_WithChanges_AndDeliversWebhookOnlyOnChange()
    {
        // Two players tracked, then flex mutates — a scan must see the change and notify.
        foreach (var username in new[] { "flex", "talon" })
        {
            (await _client.GetAsync($"/api/v1/players/lookup?username={username}")).EnsureSuccessStatusCode();
        }

        await using var receiver = await WebhookReceiver.StartAsync();
        await SeedChannelAsync("test receiver", receiver.Url);

        var create = await _client.PostAsJsonAsync("/api/v1/scans/schedules",
            new { name = "Change watcher", kind = "player-refresh", intervalMinutes = 5, batchSize = 5 });
        create.EnsureSuccessStatusCode();
        var scheduleId = JsonDocument.Parse(await create.Content.ReadAsStreamAsync()).RootElement.GetProperty("id").GetGuid();

        // First run: nothing has changed since import — no changes, no notification.
        var quiet = await _client.PostAsync($"/api/v1/scans/schedules/{scheduleId}/run", content: null);
        quiet.EnsureSuccessStatusCode();
        var quietRun = JsonDocument.Parse(await quiet.Content.ReadAsStreamAsync()).RootElement;
        Assert.Equal("ok", quietRun.GetProperty("status").GetString());
        Assert.Equal(0, quietRun.GetProperty("changesDetected").GetInt32());
        Assert.False(await receiver.ReceivedAfterAsync(TimeSpan.FromSeconds(3))); // still silent

        // Mutate flex, run again: changes detected → webhook fires with the schedule name.
        (await _mockControl.PutAsJsonAsync("/_test/players/flex", new { level = 55 })).EnsureSuccessStatusCode();
        var run = await _client.PostAsync($"/api/v1/scans/schedules/{scheduleId}/run", content: null);
        run.EnsureSuccessStatusCode();
        var runBody = JsonDocument.Parse(await run.Content.ReadAsStreamAsync()).RootElement;
        Assert.Equal("ok", runBody.GetProperty("status").GetString());
        Assert.True(runBody.GetProperty("playersObserved").GetInt32() >= 1);
        Assert.True(runBody.GetProperty("changesDetected").GetInt32() >= 1);
        var line = Assert.Single(runBody.GetProperty("changes").EnumerateArray());
        Assert.Equal("flex", line.GetProperty("player").GetString());
        // The "what changed" travels with the run: field + old → new.
        var field = Assert.Single(line.GetProperty("fields").EnumerateArray());
        Assert.Equal("level", field.GetProperty("field").GetString());
        Assert.Equal("42", field.GetProperty("oldValue").GetString());
        Assert.Equal("55", field.GetProperty("newValue").GetString());

        var payload = await receiver.NextAsync();
        Assert.Contains("Change watcher", payload.GetProperty("content").GetString());
        var embedField = Assert.Single(payload.GetProperty("embeds")[0].GetProperty("fields").EnumerateArray());
        Assert.Equal("flex", embedField.GetProperty("name").GetString());
        Assert.Contains("level: 42 → 55", embedField.GetProperty("value").GetString());

        // Run history reflects both runs, newest first.
        var runs = await GetJsonAsync("/api/v1/scans/runs");
        Assert.Equal(2, runs.GetArrayLength());
        Assert.Equal("Change watcher", runs[0].GetProperty("scheduleName").GetString());

        // The channel records its delivery outcome.
        var channels = await GetJsonAsync("/api/v1/scans/notifications/channels");
        var stored = Assert.Single(channels.EnumerateArray());
        Assert.NotNull(stored.GetProperty("lastDeliveryAt"));
        Assert.StartsWith("204", stored.GetProperty("lastDeliveryStatus").GetString());
    }

    [Fact]
    public async Task TestChannelEndpoint_DeliversFixedPayload()
    {
        await using var receiver = await WebhookReceiver.StartAsync();
        var channel = await SeedChannelAsync("probe", receiver.Url);

        var test = await _client.PostAsync($"/api/v1/scans/notifications/channels/{channel.Id}/test", content: null);
        test.EnsureSuccessStatusCode();
        var result = JsonDocument.Parse(await test.Content.ReadAsStreamAsync()).RootElement;
        Assert.True(result.GetProperty("success").GetBoolean());

        var payload = await receiver.NextAsync();
        Assert.Contains("test notification", payload.GetProperty("content").GetString());
    }

    private async Task<NotificationChannel> SeedChannelAsync(string name, string url)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<INotificationStore>();
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = NotificationChannelKinds.Webhook,
            TargetUrl = url,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await store.AddAsync(channel);
        return channel;
    }

    /// <summary>A real local HTTP receiver capturing webhook bodies (mirrors MockWolvesvilleServer's host trick).</summary>
    private sealed class WebhookReceiver : IAsyncDisposable
    {
        private readonly TaskCompletionSource<JsonElement> _payload = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _delivered;
        private WebApplication _app = null!;

        public string Url { get; private set; } = null!;

        public static async Task<WebhookReceiver> StartAsync()
        {
            var receiver = new WebhookReceiver();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            app.MapPost("/hook", async (HttpContext ctx) =>
            {
                receiver._delivered = true;
                receiver._payload.TrySetResult((await JsonDocument.ParseAsync(ctx.Request.Body)).RootElement.Clone());
                ctx.Response.StatusCode = 204;
            });
            await app.StartAsync();

            receiver._app = app;
            receiver.Url = app.Services
                .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/hook";
            return receiver;
        }

        /// <summary>True when a webhook arrived within the given wait (used to assert silence).</summary>
        public async Task<bool> ReceivedAfterAsync(TimeSpan wait)
        {
            await Task.Delay(wait);
            return _delivered;
        }

        public async Task<JsonElement> NextAsync() => await _payload.Task.WaitAsync(TimeSpan.FromSeconds(15));

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }
    }
}
