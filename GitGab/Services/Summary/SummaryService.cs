using GitGab.Models.Config;
using GitGab.Models.Connector;
using GitGab.Models.Git;
using GitGab.Models.LLM;
using GitGab.Services.LLM;
using Microsoft.Extensions.Logging;

namespace GitGab.Services.Summary;

public class SummaryService
{
    private readonly LLMProviderFactory _providerFactory;
    private readonly PromptBuilder _promptBuilder;
    private readonly ConfigurationService _configService;
    private readonly ILogger<SummaryService> _logger;

    public SummaryService(
        LLMProviderFactory providerFactory,
        PromptBuilder promptBuilder,
        ConfigurationService configService,
        ILogger<SummaryService> logger)
    {
        _providerFactory = providerFactory;
        _promptBuilder = promptBuilder;
        _configService = configService;
        _logger = logger;
    }

    public async Task<ConnectorMessage> GenerateSummaryAsync(
        DiffResult diffResult,
        string? providerName = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Generating summary for repository {Name}", diffResult.Repository.Name);

        var llmConfig = _configService.GetLLMConfig();
        var promptConfig = _configService.GetPromptConfig();

        // Build the prompt
        var prompt = _promptBuilder.BuildPrompt(diffResult, promptConfig.Template);

        // Create LLM request
        var request = new PromptRequest
        {
            Model = llmConfig.Model,
            SystemMessage = "You are an expert software engineer.",
            Messages = new List<Message>
            {
                new Message { Role = "user", Content = prompt }
            },
            Temperature = llmConfig.Temperature,
            MaxTokens = llmConfig.MaxTokens
        };

        // Get provider and generate
        var provider = _providerFactory.CreateProvider(providerName);
        _logger.LogDebug("Using LLM provider: {Provider}", provider.Name);

        var response = await provider.GenerateAsync(request, ct);

        var message = new ConnectorMessage
        {
            RepositoryName = diffResult.Repository.Name,
            Summary = response.Content,
            DiffResult = diffResult,
            LLMUsage = response.Usage,
            LLMModel = response.Model ?? llmConfig.Model
        };

        _logger.LogDebug("Generated summary of {Length} characters", response.Content.Length);

        return message;
    }

    /// <summary>
    /// Generate a simple text summary without calling an LLM (for testing/dry-run)
    /// </summary>
    public ConnectorMessage GenerateSimpleSummary(DiffResult diffResult)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"# GitGab Summary for {diffResult.Repository.Name}");
        sb.AppendLine($"## Period: {diffResult.From} to {diffResult.To}");
        sb.AppendLine();
        sb.AppendLine("## Statistics");
        sb.AppendLine($"- **Commits:** {diffResult.Stats.TotalCommits}");
        sb.AppendLine($"- **Files Changed:** {diffResult.Stats.TotalFilesChanged}");
        sb.AppendLine($"- **Lines Added:** {diffResult.Stats.LinesAdded}");
        sb.AppendLine($"- **Lines Removed:** {diffResult.Stats.LinesRemoved}");
        sb.AppendLine();
        
        if (diffResult.Commits.Count > 0)
        {
            sb.AppendLine("## Commits");
            foreach (var commit in diffResult.Commits.Take(10)) // Limit to first 10
            {
                sb.AppendLine($"- **{commit.ShortHash}** {commit.Message} ({commit.AuthorName})");
            }
        }

        return new ConnectorMessage
        {
            RepositoryName = diffResult.Repository.Name,
            Summary = sb.ToString(),
            DiffResult = diffResult,
            LLMModel = "none (dry-run)"
        };
    }
}
