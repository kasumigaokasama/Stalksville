namespace Stalksville.Infrastructure.Ai;

public sealed class AiOptions
{
    /// <summary>"None" (deterministic template narrator, default) or "OpenAiCompatible".</summary>
    public string Provider { get; set; } = "None";

    /// <summary>OpenAI-compatible chat-completions base URL, e.g. https://api.openai.com/v1.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>Backend-only secret.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gpt-4o-mini";

    public const string SectionName = "Ai";
}
