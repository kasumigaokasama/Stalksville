namespace Stalksville.Infrastructure.Wolvesville;

public sealed class WolvesvilleOptions
{
    /// <summary>"Real" (default) or "Mock" (built-in demo dataset, no API key needed).</summary>
    public string Mode { get; set; } = "Real";

    public string BaseUrl { get; set; } = "https://api.wolvesville.com";

    /// <summary>Wolvesville bot API key. Backend-only secret — never exposed to the frontend.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Permits http/loopback/private upstream hosts so integration tests can target the local
    /// MockWolvesvilleServer. Only honored in Development; must never be enabled in production.
    /// </summary>
    public bool AllowTestHost { get; set; }

    public int MaxRequestsPerSecond { get; set; } = 8;

    /// <summary>
    /// Scaffolding for optional write operations (plan §11). No write endpoints exist in this
    /// build regardless of this flag — enabling them is a deliberate future product decision.
    /// </summary>
    public bool EnableWriteOperations { get; set; }

    public const string SectionName = "Wolvesville";

    public bool IsMock => string.Equals(Mode, "Mock", StringComparison.OrdinalIgnoreCase);
}
