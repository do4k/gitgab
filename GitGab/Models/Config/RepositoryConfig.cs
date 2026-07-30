namespace GitGab.Models.Config;

public class RepositoryConfig
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public RepositoryAuth Auth { get; set; } = new();
    public string TimeWindow { get; set; } = "P7D";
    public DiffConfig? Diff { get; set; }
}

public class RepositoryAuth
{
    public string Type { get; set; } = "https"; // or "ssh"
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }
    public string? SshKeyPath { get; set; }
}

public class DiffConfig
{
    public string? From { get; set; } // tag, branch, commit
    public string? To { get; set; }   // tag, branch, commit
}
