using GitGab.Models.Config;
using GitGab.Models.Git;
using Microsoft.Extensions.Logging;

namespace GitGab.Services.Summary;

public class PromptBuilder
{
    private readonly ILogger<PromptBuilder> _logger;

    public PromptBuilder(ILogger<PromptBuilder> logger)
    {
        _logger = logger;
    }

    public string BuildPrompt(DiffResult diffResult, string template)
    {
        var result = template
            .Replace("{{repo.name}}", diffResult.Repository.Name)
            .Replace("{{time_window}}", diffResult.From + " to " + diffResult.To)
            .Replace("{{diff_summary}}", BuildDiffSummary(diffResult));
        return result;
    }

    private string BuildDiffSummary(DiffResult diffResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Total commits: " + diffResult.Stats.TotalCommits);
        sb.AppendLine("Files changed: " + diffResult.Stats.TotalFilesChanged);
        sb.AppendLine("Lines added: " + diffResult.Stats.LinesAdded);
        sb.AppendLine("Lines removed: " + diffResult.Stats.LinesRemoved);
        sb.AppendLine();
        
        if (diffResult.Stats.FilesByExtension.Count > 0)
        {
            sb.AppendLine("Files by type:");
            foreach (var kvp in diffResult.Stats.FilesByExtension.OrderByDescending(kv => kv.Value))
            {
                sb.AppendLine("  - " + kvp.Key + ": " + kvp.Value + " files");
            }
            sb.AppendLine();
        }
        
        if (diffResult.Commits.Count > 0)
        {
            sb.AppendLine("Recent commits:");
            var commitCount = 0;
            foreach (var commit in diffResult.Commits.Take(5))
            {
                sb.AppendLine("  - " + commit.ShortHash + " " + commit.Message + " (" + commit.AuthorName + ")");
                commitCount++;
            }
            if (diffResult.Commits.Count > 5)
            {
                sb.AppendLine("  ... and " + (diffResult.Commits.Count - 5) + " more commits");
            }
        }
        
        return sb.ToString();
    }
}
