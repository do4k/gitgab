namespace GitGab.Models.Config;

public class LLMConfig
{
    public string Provider { get; set; } = "gemini";
    public string Model { get; set; } = "gemini-2.5-flash";
    public string? ApiKey { get; set; }
    /// <summary>
    /// Provider-specific base URL. Required for <c>local</c> (e.g. http://localhost:11434).
    /// For cloud providers this is optional — each provider hard-codes its own default.
    /// </summary>
    public string? BaseUrl { get; set; }
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 4096;
    public int TimeoutSeconds { get; set; } = 60;
}

public class PromptConfig
{
    public string Template { get; set; } = string.Empty;
}
