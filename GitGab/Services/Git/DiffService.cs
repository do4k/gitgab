using System.Xml;
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
    /// Computes a diff covering the given ISO 8601 duration window ending at HEAD.
    /// </summary>
    public DiffResult GetTimeWindowDiff(RepositoryInfo repoInfo, string timeWindow)
    {
        var duration = ParseTimeWindow(timeWindow);
        var fromDate = DateTimeOffset.UtcNow - duration;
        var fromSpec = fromDate.ToString("yyyy-MM-ddTHH:mm:ssZ");

        _logger.LogInformation("Getting diff for {Name} from {Start} (window {Window})",
            repoInfo.Name, fromSpec, timeWindow);

        return _gitService.GetDiff(repoInfo, fromSpec, "HEAD");
    }

    /// <summary>
    /// Computes a diff between two explicit refs (SHA, branch, tag, or date string).
    /// </summary>
    public DiffResult GetRefDiff(RepositoryInfo repoInfo, string fromRef, string toRef)
    {
        return _gitService.GetDiff(repoInfo, fromRef, toRef);
    }

    /// <summary>
    /// Computes a diff from the most recent tag to HEAD.
    /// </summary>
    public DiffResult GetDiffSinceLastTag(RepositoryInfo repoInfo)
    {
        return _gitService.GetDiff(repoInfo, "last-tag", "HEAD");
    }

    /// <summary>
    /// Parses an ISO 8601 duration string (e.g. "P7D", "PT6H", "P1DT30M") into a
    /// <see cref="TimeSpan"/>. Falls back to 7 days for null/empty/invalid input.
    /// </summary>
    internal TimeSpan ParseTimeWindow(string timeWindow)
    {
        if (string.IsNullOrWhiteSpace(timeWindow))
            return TimeSpan.FromDays(7);

        try
        {
            return XmlConvert.ToTimeSpan(timeWindow);
        }
        catch (FormatException)
        {
            _logger.LogWarning("Unrecognised time window '{Window}', falling back to 7 days", timeWindow);
            return TimeSpan.FromDays(7);
        }
    }
}
