namespace GitGab.Models.Git;

public class RepositoryInfo
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public bool IsCloned { get; set; }
}
