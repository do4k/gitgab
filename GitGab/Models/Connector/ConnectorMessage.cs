using GitGab.Models.Git;
using GitGab.Models.LLM;

namespace GitGab.Models.Connector;

public class ConnectorMessage
{
    public string RepositoryName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DiffResult? DiffResult { get; set; }
    public UsageInfo? LLMUsage { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string LLMModel { get; set; } = string.Empty;
}

public class ConnectorResult
{
    public string ConnectorName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
