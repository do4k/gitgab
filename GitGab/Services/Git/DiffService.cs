using GitGab.Models.Config;
using GitGab.Models.Git;
using GitGab.Services.Config;
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

    public DiffResult GetTimeWindowDiff(RepositoryInfo repoInfo, string timeWindow)
    {
        var (days, hours, minutes) = ParseTimeWindow(timeWindow);
        var endDate = DateTimeOffset.UtcNow;
        var startDate = endDate - TimeSpan.FromDays(days) - TimeSpan.FromHours(hours) - TimeSpan.FromMinutes(minutes);

        _logger.LogInformation("Getting diff for {Name} from {Start} to {End}", 
            repoInfo.Name, startDate, endDate);

        var fromSpec = startDate.ToString("yyyy-MM-dd");
        var toSpec = "HEAD";

        return _gitService.GetDiff(repoInfo, fromSpec, toSpec);
    }

    public DiffResult GetRefDiff(RepositoryInfo repoInfo, string fromRef, string toRef)
    {
        return _gitService.GetDiff(repoInfo, fromRef, toRef);
    }

    public DiffResult GetDiffSinceLastTag(RepositoryInfo repoInfo)
    {
        return _gitService.GetDiff(repoInfo, "last-tag", "HEAD");
    }

    private (int days, int hours, int minutes) ParseTimeWindow(string timeWindow)
    {
        if (string.IsNullOrEmpty(timeWindow) || timeWindow == "P7D")
        {
            return (7, 0, 0);
        }

        var days = 0;
        var hours = 0;
        var minutes = 0;

        if (timeWindow.StartsWith("P"))
        {
            var rest = timeWindow.Substring(1);
            var parts = rest.Split('T');
            
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
                    var minStr = timeParts.Substring(minStart, minuteIndex - minStart).Replace("H", "");
                    if (int.TryParse(minStr, out var m))
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
