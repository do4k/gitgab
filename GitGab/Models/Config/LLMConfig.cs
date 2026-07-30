namespace GitGab.Models.Config;

public class LLMConfig
{
    public string Provider { get; set; } = "gemini";
    public string Model { get; set; } = "gemini-2.5-flash";
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 4096;
    public int TimeoutSeconds { get; set; } = 60;
}

public class PromptConfig
{
    public string Template { get; set; } = string.Empty;
}
