using System.CommandLine;
using GitGab.Models.Config;
using GitGab.Services.Config;
using GitGab.Services.Connector;
using GitGab.Services.Git;
using GitGab.Services.Summary;
using Microsoft.Extensions.DependencyInjection;

namespace GitGab.Commands;

public class AnalyzeCommand : Command
{
    private readonly IServiceProvider _services;

    public AnalyzeCommand(IServiceProvider services) : base("analyze", "Analyze repository changes and generate summary")
    {
        _services = services;

        AddOption(new Option<string>(["--repo", "-r"], "Repository name to analyze"));
        AddOption(new Option<bool>(["--all", "-a"], "Analyze all repositories"));
        AddOption(new Option<string>(["--time-window", "-t"], "Time window for diff (e.g., P7D)") { DefaultValue = "P7D" });
        AddOption(new Option<string>(["--from"], "From commit/branch/tag"));
        AddOption(new Option<string>(["--to"], "To commit/branch/tag"));
        AddOption(new Option<string[]>(["--connector", "-c"], "Connectors to send output to"));
        AddOption(new Option<bool>(["--dry-run"], "Generate summary without sending to connectors"));
        AddOption(new Option<string>(["--output", "-o"], "Output format"));

        this.SetHandler(HandleAnalyzeAsync);
    }

    private async Task<int> HandleAnalyzeAsync(AnalyzeCommandContext context)
    {
        var logger = _services.GetRequiredService<ILogger<AnalyzeCommand>>();
        var configService = _services.GetRequiredService<ConfigurationService>();
        var gitService = _services.GetRequiredService<GitService>();
        var diffService = _services.GetRequiredService<DiffService>();
        var summaryService = _services.GetRequiredService<SummaryService>();
        var connectorFactory = _services.GetRequiredService<ConnectorFactory>();

        try
        {
            // Get repositories to analyze
            var repos = configService.GetRepositories();
            var reposToAnalyze = new List<RepositoryConfig>();

            if (context.Repo != null)
            {
                var repo = repos.FirstOrDefault(r => r.Name.Equals(context.Repo, StringComparison.OrdinalIgnoreCase));
                if (repo == null)
                {
                    logger.LogError("Repository {Name} not found", context.Repo);
                    return 1;
                }
                reposToAnalyze.Add(repo);
            }
            else if (context.All)
            {
                reposToAnalyze = repos;
            }
            else if (repos.Count == 1)
            {
                reposToAnalyze = repos;
            }
            else
            {
                logger.LogError("No repository specified and multiple repositories configured. Use --repo or --all");
                return 1;
            }

            // Analyze each repository
            foreach (var repoConfig in reposToAnalyze)
            {
                logger.LogInformation("Analyzing repository: {Name}", repoConfig.Name);

                // Clone or pull repository
                var repoInfo = await Task.Run(() => gitService.CloneRepository(repoConfig));
                await Task.Run(() => gitService.PullRepository(repoInfo));

                // Get diff
                DiffResult diffResult;
                if (!string.IsNullOrEmpty(context.From) && !string.IsNullOrEmpty(context.To))
                {
                    diffResult = diffService.GetRefDiff(repoInfo, context.From, context.To);
                }
                else
                {
                    diffResult = diffService.GetTimeWindowDiff(repoInfo, context.TimeWindow);
                }

                // Generate summary
                ConnectorMessage message;
                if (context.DryRun)
                {
                    logger.LogInformation("Dry run - generating simple summary without LLM");
                    message = summaryService.GenerateSimpleSummary(diffResult);
                }
                else
                {
                    message = await summaryService.GenerateSummaryAsync(diffResult, context.LLMProvider);
                }

                // Send to connectors
                if (!context.DryRun || context.Output != null)
                {
                    var connectors = context.Connectors?.Length > 0
                        ? context.Connectors.Select(c => connectorFactory.CreateConnector(c)).ToList()
                        : connectorFactory.CreateAllConnectors();

                    foreach (var connector in connectors)
                    {
                        logger.LogInformation("Sending to connector: {Name}", connector.Name);
                        var result = await connector.SendAsync(message, context.CancellationToken);
                        if (!result.Success)
                        {
                            logger.LogError("Failed to send to connector {Name}: {Error}", connector.Name, result.ErrorMessage);
                        }
                    }
                }
                else
                {
                    // In dry-run mode with no output specified, just print to console
                    Console.WriteLine();
                    Console.WriteLine(message.Summary);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during analysis");
            return 1;
        }
    }

    private class AnalyzeCommandContext
    {
        public string? Repo { get; set; }
        public bool All { get; set; }
        public string TimeWindow { get; set; } = "P7D";
        public string? From { get; set; }
        public string? To { get; set; }
        public string[]? Connectors { get; set; }
        public bool DryRun { get; set; }
        public string? Output { get; set; }
        public string? LLMProvider { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }
}
