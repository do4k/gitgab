using GitGab.Models.Config;
using GitGab.Models.Git;
using Microsoft.Extensions.Logging;

namespace GitGab.Services.Git;

public class DiffService
{
    private readonly GitService _gitService;
    private readonly ILogger<DiffService> _logger;

    public DiffService(GitService gitService, ILogger<DiffService> logger)
    {
        _gitService = gitService;
        _logger = logger;
    }

    /// <summary>
    /// Get diff for a repository over a time window
    /// </summary>
    public DiffResult GetTimeWindowDiff(RepositoryInfo repoInfo, string timeWindow)
    {
        // Parse time window (ISO 8601 duration format like P7D, P1M, etc.)
        var (days, hours, minutes) = ParseTimeWindow(timeWindow);
        var endDate = DateTimeOffset.UtcNow;
        var startDate = endDate - TimeSpan.FromDays(days) - TimeSpan.FromHours(hours) - TimeSpan.FromMinutes(minutes);

        _logger.LogInformation("Getting diff for {Name} from {Start} to {End}", 
            repoInfo.Name, startDate, endDate);

        // Get the commit at start date (find first commit after startDate)
        var fromSpec = startDate.ToString("yyyy-MM-dd");
        var toSpec = "HEAD";

        return _gitService.GetDiff(repoInfo, fromSpec, toSpec);
    }

    /// <summary>
    /// Get diff between two specific refs (branches, tags, commits)
    /// </summary>
    public DiffResult GetRefDiff(RepositoryInfo repoInfo, string fromRef, string toRef)
    {
        return _gitService.GetDiff(repoInfo, fromRef, toRef);
    }

    /// <summary>
    /// Get diff since last tag
    /// </summary>
    public DiffResult GetDiffSinceLastTag(RepositoryInfo repoInfo)
    {
        return _gitService.GetDiff(repoInfo, "last-tag", "HEAD");
    }

    private (int days, int hours, int minutes) ParseTimeWindow(string timeWindow)
    {
        if (string.IsNullOrEmpty(timeWindow) || timeWindow == "P7D")
        {
            return (7, 0, 0); // Default to 7 days
        }

        // Simple parser for ISO 8601 duration
        // Format: P[n]Y[n]M[n]DT[n]H[n]M[n]S
        // We only handle days, hours, minutes for simplicity
        
        var days = 0;
        var hours = 0;
        var minutes = 0;

        if (timeWindow.StartsWith("P"))
        {
            var rest = timeWindow.Substring(1);
            var parts = rest.Split('T');
            
            // Parse date part (before T)
            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
            {
                var dateParts = parts[0];
                var dayIndex = dateParts.IndexOf("D");
                if (dayIndex > 0)
                {
                    if (int.TryParse(dateParts.Substring(0, dayIndex), out var d))
                    {
                        days = d;
                    }
                }
            }

            // Parse time part (after T)
            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
            {
                var timeParts = parts[1];
                var hourIndex = timeParts.IndexOf("H");
                var minuteIndex = timeParts.IndexOf("M");

                if (hourIndex > 0)
                {
                    if (int.TryParse(timeParts.Substring(0, hourIndex), out var h))
                    {
                        hours = h;
                    }
                }

                if (minuteIndex > hourIndex)
                {
                    var minStart = hourIndex > 0 ? hourIndex : 0;
                    if (int.TryParse(timeParts.Substring(minStart, minuteIndex - minStart).Replace("H", ""), out var m))
                    {
                        minutes = m;
                    }
                }
                else if (minuteIndex > 0 && hourIndex < 0)
                {
                    if (int.TryParse(timeParts.Substring(0, minuteIndex), out var m))
                    {
                        minutes = m;
                    }
                }
            }
        }

        return (days, hours, minutes);
    }
}
