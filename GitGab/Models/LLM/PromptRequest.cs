namespace GitGab.Models.LLM;

public class PromptRequest
{
    public string Model { get; set; } = string.Empty;
    public string SystemMessage { get; set; } = string.Empty;
    public List<Message> Messages { get; set; } = new();
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 4096;
}

public class Message
{
    public string Role { get; set; } = "user"; // or "assistant", "system"
    public string Content { get; set; } = string.Empty;
}

public class PromptResponse
{
    public string Content { get; set; } = string.Empty;
    public UsageInfo Usage { get; set; } = new();
    public string? Model { get; set; }
}

public class UsageInfo
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
}
