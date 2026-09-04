using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Stalksville.Application.Abstractions;
using Stalksville.Domain.Entities;
using Stalksville.Infrastructure.Wolvesville;

namespace Stalksville.Infrastructure.Notifications;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Development/test escape hatch for the outbound host guard (never enable in production).</summary>
    public bool AllowTestHost { get; set; }

    /// <summary>Per-request delivery timeout.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Delivers scan notifications as HTTP POST webhooks. The payload is Discord-compatible
/// ({content, embeds}) so a Discord webhook URL works out of the box while remaining plain JSON
/// for generic receivers. Every outbound call passes the same host guard as the Wolvesville
/// client: https-only, no loopback/private/reserved hosts (unless test hosts are allowed).
/// </summary>
public sealed class WebhookDispatcher(
    HttpClient http,
    IOptions<NotificationsOptions> options,
    IHostEnvironment environment) : INotificationDispatcher
{
    private static readonly JsonSerializerOptions PayloadJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<(bool Success, string Detail)> SendAsync(NotificationChannel channel, ScanNotification notification, CancellationToken cancellationToken = default)
    {
        if (channel.Kind != NotificationChannelKinds.Webhook)
        {
            return (false, $"unsupported channel kind '{channel.Kind}'");
        }

        if (!Uri.TryCreate(channel.TargetUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return (false, "invalid webhook URL");
        }

        var allowTestHosts = options.Value.AllowTestHost && environment.IsDevelopment();
        try
        {
            HostGuard.Validate(uri, allowTestHosts);
        }
        catch (InvalidOperationException ex)
        {
            return (false, $"blocked: {ex.Message}");
        }

        using var response = await http.PostAsync(
            uri,
            new StringContent(JsonSerializer.Serialize(BuildPayload(notification), PayloadJson), Encoding.UTF8, "application/json"),
            cancellationToken);

        var detail = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        return response.IsSuccessStatusCode ? (true, detail) : (false, detail);
    }

    /// <summary>Discord webhook shape: short content line plus one embed with the run summary.</summary>
    private static object BuildPayload(ScanNotification n) => new
    {
        username = "Stalksville",
        allowed_mentions = new { parse = Array.Empty<string>() },
        content = n.IsTest
            ? $"✅ Stalksville test notification for channel \"{n.ScheduleName}\" — deliveries are working."
            : $"🔎 Stalksville scan \"{n.ScheduleName}\" detected {n.ChangesDetected} change(s)",
        embeds = new object[]
        {
            new
            {
                title = n.IsTest ? "Stalksville — channel test" : $"Scan results — {n.ScheduleName}",
                description = n.IsTest
                    ? "If you can read this, the channel is configured correctly. Real notifications fire only when a scheduled scan detects changes."
                    : $"{n.PlayersObserved} observed · {n.ChangesDetected} change(s) · {n.AlertsRaised} alert(s) · {n.Kind}",
                color = 0x7c9aff,
                fields = n.Changes.Take(20).Select(line => new
                {
                    name = line.Player,
                    value = DescribeFields(line),
                    @inline = false,
                }).ToArray(),
                timestamp = n.FinishedAt.ToString("O"),
            },
        },
    };

    /// <summary>"level: 42 → 55" lines — the "which player changed what" the notification answers.</summary>
    private static string DescribeFields(ScanChangeLine line)
    {
        var fields = line.Fields ?? [];
        if (fields.Count == 0)
        {
            return $"{line.Changes} change(s)";
        }

        var text = string.Join("\n", fields.Select(f => $"{f.Field}: {DescribeValue(f.OldValue)} → {DescribeValue(f.NewValue)}"));
        return text.Length > 1000 ? text[..997] + "…" : text;
    }

    private static string DescribeValue(string? value) => string.IsNullOrEmpty(value) ? "none" : value;
}
