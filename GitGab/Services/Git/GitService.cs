using GitGab.Models.Config;
using GitGab.Models.Git;
using GitGab.Services.Config;
using Microsoft.Extensions.Logging;

namespace GitGab.Services.Git;

public class GitService
{
    private readonly ILogger<GitService> _logger;
    private readonly ConfigurationService _configService;
    private readonly string _repoCacheDir;

    public GitService(ILogger<GitService> logger, ConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;
        var appSettings = _configService.GetAppSettings();
        _repoCacheDir = appSettings.RepoCacheDir;

        if (!Directory.Exists(_repoCacheDir))
        {
            Directory.CreateDirectory(_repoCacheDir);
        }
    }

    public RepositoryInfo CloneRepository(RepositoryConfig config)
    {
        var repoInfo = new RepositoryInfo
        {
            Name = config.Name,
            Url = config.Url,
            Branch = config.Branch,
            LocalPath = Path.Combine(_repoCacheDir, config.Name),
            IsCloned = false
        };

        if (Directory.Exists(repoInfo.LocalPath))
        {
            _logger.LogInformation("Repository {Name} already exists at {Path}", config.Name, repoInfo.LocalPath);
            repoInfo.IsCloned = true;
            return repoInfo;
        }

        _logger.LogInformation("Cloning repository {Name} from {Url}", config.Name, config.Url);
        _logger.LogWarning("Git clone implementation not yet complete - returning stub");
        
        return repoInfo;
    }

    public void PullRepository(RepositoryInfo repoInfo)
    {
        if (!repoInfo.IsCloned || !Directory.Exists(repoInfo.LocalPath))
        {
            _logger.LogWarning("Repository {Name} is not cloned, cannot pull", repoInfo.Name);
            return;
        }

        _logger.LogInformation("Pulling latest changes for {Name}", repoInfo.Name);
        _logger.LogWarning("Git pull implementation not yet complete");
    }

    public DiffResult GetDiff(RepositoryInfo repoInfo, string fromSpec, string toSpec)
    {
        if (!repoInfo.IsCloned || !Directory.Exists(repoInfo.LocalPath))
        {
            throw new InvalidOperationException($"Repository {repoInfo.Name} is not cloned");
        }

        _logger.LogWarning("Diff implementation using LibGit2Sharp not yet complete");
        
        return new DiffResult
        {
            Repository = repoInfo,
            From = fromSpec,
            To = toSpec,
            FromDate = DateTimeOffset.UtcNow - TimeSpan.FromDays(7),
            ToDate = DateTimeOffset.UtcNow,
            Commits = new List<CommitInfo>(),
            Stats = new GitStats()
        };
    }
}
