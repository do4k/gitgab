namespace GitGab.Models.Git;

public class CommitInfo
{
    public string Hash { get; set; } = string.Empty;
    public string ShortHash => Hash[..7];
    public string Message { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public DateTimeOffset AuthorDate { get; set; }
    public List<FileChange> Changes { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

public class FileChange
{
    public string Path { get; set; } = string.Empty;
    public ChangeType Type { get; set; }
    public int LinesAdded { get; set; }
    public int LinesRemoved { get; set; }
    public string? Diff { get; set; }
}

public enum ChangeType
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied
}
