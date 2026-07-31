using GitGab.Models.Config;
using GitGab.Models.Git;
using GitGab.Services.Config;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;

// Alias LibGit2Sharp.Commands to avoid collision with the GitGab.Commands namespace.
using LibGitCommands = LibGit2Sharp.Commands;

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

    /// <summary>
    /// Clones <paramref name="config"/> into the repo cache directory, or marks it as
    /// already-cloned if the directory already contains a valid git repository.
    /// </summary>
    public RepositoryInfo CloneRepository(RepositoryConfig config)
    {
        var localPath = Path.Combine(_repoCacheDir, config.Name);

        var repoInfo = new RepositoryInfo
        {
            Name = config.Name,
            Url = config.Url,
            Branch = config.Branch,
            LocalPath = localPath,
            IsCloned = false
        };

        // Already a valid git repo on disk — skip clone
        if (Directory.Exists(localPath) && Repository.IsValid(localPath))
        {
            _logger.LogInformation("Repository {Name} already cloned at {Path}", config.Name, localPath);
            repoInfo.IsCloned = true;
            return repoInfo;
        }

        _logger.LogInformation("Cloning {Url} → {Path}", config.Url, localPath);

        try
        {
            var cloneOptions = BuildCloneOptions(config);
            Repository.Clone(config.Url, localPath, cloneOptions);
            repoInfo.IsCloned = true;
            _logger.LogInformation("Cloned {Name} successfully", config.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clone {Name} from {Url}", config.Name, config.Url);
            throw new InvalidOperationException($"Failed to clone repository '{config.Name}': {ex.Message}", ex);
        }

        return repoInfo;
    }

    /// <summary>
    /// Fetches and fast-forwards (pulls) the tracked remote branch.
    /// </summary>
    public void PullRepository(RepositoryInfo repoInfo, RepositoryConfig config)
    {
        if (!repoInfo.IsCloned || !Directory.Exists(repoInfo.LocalPath))
        {
            _logger.LogWarning("Repository {Name} is not cloned — skipping pull", repoInfo.Name);
            return;
        }

        _logger.LogInformation("Pulling latest changes for {Name}", repoInfo.Name);

        try
        {
            using var repo = new Repository(repoInfo.LocalPath);

            var fetchOptions = new FetchOptions
            {
                CredentialsProvider = BuildCredentialsHandler(config)
            };

            // Fetch from all remotes
            foreach (var remote in repo.Network.Remotes)
            {
                var refSpecs = remote.FetchRefSpecs.Select(r => r.Specification);
                LibGitCommands.Fetch(repo, remote.Name, refSpecs, fetchOptions, "fetch");
            }

            // Pull using Commands.Pull which handles merge/rebase according to config
            var signature = new Signature("GitGab", "gitgab@local", DateTimeOffset.UtcNow);
            var pullOptions = new PullOptions
            {
                FetchOptions = fetchOptions,
                MergeOptions = new MergeOptions { FastForwardStrategy = FastForwardStrategy.FastForwardOnly }
            };

            LibGitCommands.Pull(repo, signature, pullOptions);
            _logger.LogInformation("Pulled {Name} successfully", repoInfo.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pull {Name}", repoInfo.Name);
            throw new InvalidOperationException($"Failed to pull repository '{repoInfo.Name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Computes the diff between two refs/dates.
    /// <paramref name="fromSpec"/> may be a SHA, branch name, tag, or ISO date string (yyyy-MM-dd).
    /// <paramref name="toSpec"/> defaults to HEAD.
    /// </summary>
    public DiffResult GetDiff(RepositoryInfo repoInfo, string fromSpec, string toSpec = "HEAD")
    {
        if (!repoInfo.IsCloned || !Directory.Exists(repoInfo.LocalPath))
            throw new InvalidOperationException($"Repository '{repoInfo.Name}' is not cloned at '{repoInfo.LocalPath}'");

        _logger.LogInformation("Computing diff for {Name}: {From} → {To}", repoInfo.Name, fromSpec, toSpec);

        using var repo = new Repository(repoInfo.LocalPath);

        // Resolve the "to" commit
        var toCommit = ResolveCommit(repo, toSpec)
            ?? throw new InvalidOperationException($"Cannot resolve '{toSpec}' in repository '{repoInfo.Name}'");

        // Resolve the "from" — may be a date string or a ref
        Commit? fromCommit;
        DateTimeOffset fromDate;

        if (DateTimeOffset.TryParse(fromSpec, out var parsedDate))
        {
            // Date-based: find the most recent commit on or before this date
            fromCommit = repo.Commits
                .QueryBy(new CommitFilter { IncludeReachableFrom = toCommit, SortBy = CommitSortStrategies.Time })
                .FirstOrDefault(c => c.Author.When <= parsedDate);
            fromDate = parsedDate;
        }
        else
        {
            fromCommit = ResolveCommit(repo, fromSpec);
            fromDate = fromCommit?.Author.When ?? toCommit.Author.When - TimeSpan.FromDays(7);
        }

        // Collect commits in the range
        var commitFilter = new CommitFilter
        {
            IncludeReachableFrom = toCommit,
            ExcludeReachableFrom = fromCommit,
            SortBy = CommitSortStrategies.Time
        };

        var commits = repo.Commits.QueryBy(commitFilter).ToList();

        // Build diff between the two trees
        var fromTree = fromCommit?.Tree;
        var toTree = toCommit.Tree;

        var compareOptions = new CompareOptions { ContextLines = 3 };
        var treeChanges = fromTree != null
            ? repo.Diff.Compare<TreeChanges>(fromTree, toTree, compareOptions)
            : repo.Diff.Compare<TreeChanges>(null, toTree, compareOptions);

        var stats = BuildStats(commits, treeChanges, repo, fromTree, toTree);
        var commitInfos = commits.Select(c => MapCommit(c, repo)).ToList();

        return new DiffResult
        {
            Repository = repoInfo,
            From = fromSpec,
            To = toSpec,
            FromDate = fromDate,
            ToDate = toCommit.Author.When,
            Commits = commitInfos,
            Stats = stats
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Commit? ResolveCommit(Repository repo, string spec)
    {
        if (spec.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
            return repo.Head.Tip;

        // Try as a direct SHA or partial SHA
        if (repo.Lookup<Commit>(spec) is { } byHash)
            return byHash;

        // Try as a branch
        var branch = repo.Branches[spec] ?? repo.Branches[$"origin/{spec}"];
        if (branch?.Tip != null)
            return branch.Tip;

        // Try as a tag
        var tag = repo.Tags[spec];
        if (tag?.Target is Commit tagCommit)
            return tagCommit;
        if (tag?.Target is TagAnnotation annotated && annotated.Target is Commit taggedCommit)
            return taggedCommit;

        return null;
    }

    private static GitStats BuildStats(
        List<Commit> commits,
        TreeChanges treeChanges,
        Repository repo,
        Tree? fromTree,
        Tree toTree)
    {
        var filesByExt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byAuthor = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int linesAdded = 0, linesRemoved = 0;

        foreach (var change in treeChanges)
        {
            var ext = Path.GetExtension(change.Path);
            if (!string.IsNullOrEmpty(ext))
                filesByExt[ext] = filesByExt.GetValueOrDefault(ext) + 1;
        }

        // Compute line stats via patch
        var patch = fromTree != null
            ? repo.Diff.Compare<Patch>(fromTree, toTree)
            : repo.Diff.Compare<Patch>(null, toTree);

        linesAdded = patch.LinesAdded;
        linesRemoved = patch.LinesDeleted;

        foreach (var commit in commits)
        {
            var author = commit.Author.Name;
            byAuthor[author] = byAuthor.GetValueOrDefault(author) + 1;
        }

        return new GitStats
        {
            TotalCommits = commits.Count,
            TotalFilesChanged = treeChanges.Count(),
            LinesAdded = linesAdded,
            LinesRemoved = linesRemoved,
            FilesByExtension = filesByExt,
            CommitsByAuthor = byAuthor
        };
    }

    private static CommitInfo MapCommit(Commit commit, Repository repo)
    {
        var changes = new List<FileChange>();

        if (commit.Parents.Any())
        {
            var parent = commit.Parents.First();
            var diff = repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree);

            foreach (var change in diff)
            {
                changes.Add(new FileChange
                {
                    Path = change.Path,
                    Type = MapChangeKind(change.Status)
                });
            }
        }

        return new CommitInfo
        {
            Hash = commit.Sha,
            Message = commit.MessageShort,
            AuthorName = commit.Author.Name,
            AuthorEmail = commit.Author.Email,
            AuthorDate = commit.Author.When,
            Tags = repo.Tags
                .Where(t => t.Target.Sha == commit.Sha)
                .Select(t => t.FriendlyName)
                .ToList(),
            Changes = changes
        };
    }

    private static ChangeType MapChangeKind(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => ChangeType.Added,
        ChangeKind.Deleted => ChangeType.Deleted,
        ChangeKind.Renamed => ChangeType.Renamed,
        ChangeKind.Copied => ChangeType.Copied,
        _ => ChangeType.Modified
    };

    private static CloneOptions BuildCloneOptions(RepositoryConfig config)
    {
        var fetchOptions = new FetchOptions
        {
            CredentialsProvider = BuildCredentialsHandler(config)
        };
        return new CloneOptions(fetchOptions)
        {
            BranchName = config.Branch
        };
    }

    private static CredentialsHandler BuildCredentialsHandler(RepositoryConfig config)
    {
        return (url, usernameFromUrl, supportedCredentialTypes) =>
        {
            // HTTPS token auth (GitHub/GitLab style: token as password)
            if (!string.IsNullOrEmpty(config.Auth.Token))
            {
                return new UsernamePasswordCredentials
                {
                    Username = config.Auth.Username ?? "oauth2",
                    Password = config.Auth.Token
                };
            }

            // HTTPS username + password
            if (!string.IsNullOrEmpty(config.Auth.Username) && !string.IsNullOrEmpty(config.Auth.Password))
            {
                return new UsernamePasswordCredentials
                {
                    Username = config.Auth.Username,
                    Password = config.Auth.Password
                };
            }

            // SSH — use DefaultCredentials, which lets libgit2 delegate to the
            // system ssh-agent / ssh-askpass (SshAgentCredentials was removed in
            // LibGit2Sharp 0.30+; DefaultCredentials is the correct replacement).
            if (config.Auth.Type.Equals("ssh", StringComparison.OrdinalIgnoreCase))
            {
                return new DefaultCredentials();
            }

            return new DefaultCredentials();
        };
    }
}
