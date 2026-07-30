namespace GitGab.Models.Config;

public class AppSettings
{
    public string Name { get; set; } = "GitGab";
    public string LogLevel { get; set; } = "Information";
    public string RepoCacheDir { get; set; } = "./cache/repos";
    public string DefaultTimeWindow { get; set; } = "P7D";
    public int MaxConcurrentRepos { get; set; } = 5;
    public int MaxConcurrentLLMCalls { get; set; } = 3;
}
