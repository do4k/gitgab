namespace GitGab.Models.Git;

public class DiffResult
{
    public RepositoryInfo Repository { get; set; } = new();
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public DateTimeOffset FromDate { get; set; }
    public DateTimeOffset ToDate { get; set; }
    public List<CommitInfo> Commits { get; set; } = new();
    public GitStats Stats { get; set; } = new();
}

public class GitStats
{
    public int TotalCommits { get; set; }
    public int TotalFilesChanged { get; set; }
    public int LinesAdded { get; set; }
    public int LinesRemoved { get; set; }
    public Dictionary<string, int> FilesByExtension { get; set; } = new();
    public Dictionary<string, int> CommitsByAuthor { get; set; } = new();
}
