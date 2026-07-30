using GitGab.Models.Config;
using GitGab.Models.Git;
using LibGit2Sharp;
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
            LocalPath = Path.Combine(_repoCacheDir, config.Name)
        };

        if (Directory.Exists(repoInfo.LocalPath))
        {
            _logger.LogInformation("Repository {Name} already exists at {Path}", config.Name, repoInfo.LocalPath);
            repoInfo.IsCloned = true;
            return repoInfo;
        }

        _logger.LogInformation("Cloning repository {Name} from {Url}", config.Name, config.Url);

        var cloneOptions = new CloneOptions
        {
            BranchName = config.Branch,
            Checkout = true
        };

        if (config.Auth.Type.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(config.Auth.Token))
            {
                cloneOptions.CredentialsProvider = (url, user, cred) =>
                    new UsernamePasswordCredentials { Username = "token", Password = config.Auth.Token };
            }
            else if (!string.IsNullOrEmpty(config.Auth.Username) && !string.IsNullOrEmpty(config.Auth.Password))
            {
                cloneOptions.CredentialsProvider = (url, user, cred) =>
                    new UsernamePasswordCredentials { Username = config.Auth.Username, Password = config.Auth.Password };
            }
        }

        Repository.Clone(config.Url, repoInfo.LocalPath, cloneOptions);
        repoInfo.IsCloned = true;
        _logger.LogInformation("Successfully cloned {Name}", config.Name);

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

        using var repo = new Repository(repoInfo.LocalPath);
        var signature = new Signature("GitGab", "gitgab@example.com", DateTimeOffset.Now);
        Commands.Pull(repo, signature, new PullOptions());
        _logger.LogInformation("Successfully pulled latest changes for {Name}", repoInfo.Name);
    }

    public DiffResult GetDiff(RepositoryInfo repoInfo, string fromSpec, string toSpec)
    {
        if (!repoInfo.IsCloned || !Directory.Exists(repoInfo.LocalPath))
        {
            throw new InvalidOperationException($"Repository {repoInfo.Name} is not cloned");
        }

        using var repo = new Repository(repoInfo.LocalPath);
        var result = new DiffResult
        {
            Repository = repoInfo,
            From = fromSpec,
            To = toSpec
        };

        // Resolve the from and to commits
        var fromCommit = ResolveCommit(repo, fromSpec);
        var toCommit = ResolveCommit(repo, toSpec);

        if (fromCommit == null || toCommit == null)
        {
            throw new InvalidOperationException("Could not resolve commit references");
        }

        result.FromDate = fromCommit.Author.When;
        result.ToDate = toCommit.Author.When;

        // Get all commits between from and to
        var commitFilter = new CommitFilter
        {
            Since = fromCommit,
            Until = toCommit
        };

        var commits = repo.Commits.QueryBy(commitFilter).ToList();
        result.Commits = MapCommits(repo, commits);
        result.Stats = CalculateStats(result.Commits);

        return result;
    }

    private Commit? ResolveCommit(Repository repo, string spec)
    {
        // Try different ways to resolve the spec
        if (spec.StartsWith("ref:") && repo.Tags.Any(t => t.FriendlyName == spec[4..]))
        {
            return repo.Tags[spec[4..]].Target as Commit;
        }

        // Try by tag name directly
        if (repo.Tags.Any(t => t.FriendlyName == spec))
        {
            return repo.Tags[spec].Target as Commit;
        }

        // Try by branch name
        var branch = repo.Branches[spec];
        if (branch != null)
        {
            return branch.Tip;
        }

        // Try by commit hash (full or short)
        try
        {
            var obj = repo.Lookup(spec);
            return obj as Commit;
        }
        catch
        {
            // Try with full hash
            try
            {
                var fullHash = spec.Length == 40 ? spec : spec.PadRight(40, '0');
                var obj = repo.Lookup(fullHash);
                return obj as Commit;
            }
            catch { }
        }

        // Try time-based: "P7D" means 7 days ago
        if (spec.StartsWith("P") && int.TryParse(spec[1..^1], out var days))
        {
            var date = DateTimeOffset.UtcNow - TimeSpan.FromDays(days);
            var commits = repo.Commits.QueryBy(new CommitFilter { Since = date });
            return commits.FirstOrDefault()?.Commit;
        }

        return null;
    }

    private List<CommitInfo> MapCommits(Repository repo, IEnumerable<Commit> commits)
    {
        var result = new List<CommitInfo>();
        foreach (var commit in commits)
        {
            var commitInfo = new CommitInfo
            {
                Hash = commit.Sha,
                Message = commit.Message,
                AuthorName = commit.Author.Name,
                AuthorEmail = commit.Author.Email,
                AuthorDate = commit.Author.When
            };

            // Get file changes for this commit
            var parent = commit.Parents.FirstOrDefault();
            if (parent != null)
            {
                var tree = commit.Tree;
                var parentTree = parent.Tree;
                var diff = repo.Diff.Compare<Patch>(parentTree, tree);

                foreach (var patch in diff)
                {
                    var changeType = GetChangeType(patch.Status);
                    var linesAdded = patch.LineStatistics?.Added ?? 0;
                    var linesRemoved = patch.LineStatistics?.Deleted ?? 0;

                    commitInfo.Changes.Add(new FileChange
                    {
                        Path = patch.OldPath ?? patch.Path,
                        Type = changeType,
                        LinesAdded = linesAdded,
                        LinesRemoved = linesRemoved
                    });
                }
            }

            commitInfo.Tags.AddRange(commit.Tags.Select(t => t.FriendlyName));
            result.Add(commitInfo);
        }
        return result;
    }

    private ChangeType GetChangeType(ChangeKind status)
    {
        return status switch
        {
            ChangeKind.Added => ChangeType.Added,
            ChangeKind.Deleted => ChangeType.Deleted,
            ChangeKind.Modified => ChangeType.Modified,
            ChangeKind.Renamed => ChangeType.Renamed,
            ChangeKind.Copied => ChangeType.Copied,
            _ => ChangeType.Modified
        };
    }

    private GitStats CalculateStats(List<CommitInfo> commits)
    {
        var stats = new GitStats
        {
            TotalCommits = commits.Count
        };

        var filesByExtension = new Dictionary<string, int>();
        var commitsByAuthor = new Dictionary<string, int>();

        foreach (var commit in commits)
        {
            // Track commits by author
            if (!commitsByAuthor.ContainsKey(commit.AuthorEmail))
            {
                commitsByAuthor[commit.AuthorEmail] = 0;
            }
            commitsByAuthor[commit.AuthorEmail]++;

            foreach (var change in commit.Changes)
            {
                stats.LinesAdded += change.LinesAdded;
                stats.LinesRemoved += change.LinesRemoved;

                var ext = Path.GetExtension(change.Path).ToLower();
                if (!filesByExtension.ContainsKey(ext))
                {
                    filesByExtension[ext] = 0;
                }
                filesByExtension[ext]++;
            }
        }

        stats.TotalFilesChanged = commits.Sum(c => c.Changes.Count);
        stats.FilesByExtension = filesByExtension;
        stats.CommitsByAuthor = commitsByAuthor;

        return stats;
    }
}
