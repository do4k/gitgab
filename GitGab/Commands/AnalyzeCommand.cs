using System.CommandLine;
using GitGab.Services.Config;
using GitGab.Services.Connector;
using GitGab.Services.Git;
using GitGab.Services.Summary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GitGab.Commands;

public class AnalyzeCommand : Command
{
    private readonly IServiceProvider _services;

    public AnalyzeCommand(IServiceProvider services) : base("analyze", "Analyze repository changes and generate summary")
    {
        _services = services;

        var repoOption = new Option<string?>(new[] { "--repo", "-r" }, "Repository name to analyze");
        var allOption = new Option<bool>(new[] { "--all", "-a" }, "Analyze all repositories");
        var timeWindowOption = new Option<string>(new[] { "--time-window", "-t" }, () => "P7D", "ISO 8601 duration for the diff window (e.g. P7D, PT6H)");
        var fromOption = new Option<string?>(new[] { "--from" }, "From commit/branch/tag");
        var toOption = new Option<string?>(new[] { "--to" }, () => "HEAD", "To commit/branch/tag");
        var connectorOption = new Option<string[]>(new[] { "--connector", "-c" }, "Connector name(s) to send output to");
        var dryRunOption = new Option<bool>(new[] { "--dry-run" }, "Generate summary without sending to connectors");

        Add(repoOption);
        Add(allOption);
        Add(timeWindowOption);
        Add(fromOption);
        Add(toOption);
        Add(connectorOption);
        Add(dryRunOption);

        this.SetHandler(async (repo, all, timeWindow, from, to, connectors, dryRun) =>
        {
            await RunAsync(repo, all, timeWindow, from, to, connectors, dryRun);
        }, repoOption, allOption, timeWindowOption, fromOption, toOption, connectorOption, dryRunOption);
    }

    private async Task RunAsync(
        string? repoName,
        bool all,
        string timeWindow,
        string? from,
        string? to,
        string[] connectorNames,
        bool dryRun)
    {
        var logger = _services.GetRequiredService<ILogger<AnalyzeCommand>>();
        var configService = _services.GetRequiredService<ConfigurationService>();
        var gitService = _services.GetRequiredService<GitService>();
        var diffService = _services.GetRequiredService<DiffService>();
        var summaryService = _services.GetRequiredService<SummaryService>();
        var connectorFactory = _services.GetRequiredService<ConnectorFactory>();

        // Resolve which repositories to process
        var allRepos = configService.GetRepositories();

        if (allRepos.Count == 0)
        {
            logger.LogError("No repositories configured. Add entries under 'Repositories' in appsettings.json.");
            return;
        }

        var targetRepos = all
            ? allRepos
            : allRepos.Where(r => r.Name.Equals(repoName, StringComparison.OrdinalIgnoreCase)).ToList();

        if (targetRepos.Count == 0)
        {
            logger.LogError("Repository '{Name}' not found in configuration. Use --all or check the name.", repoName);
            return;
        }

        // Resolve connectors
        var allConnectors = connectorFactory.CreateAllConnectors();
        var activeConnectors = connectorNames.Length > 0
            ? allConnectors.Where(c => connectorNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase)).ToList()
            : allConnectors;

        if (!dryRun && activeConnectors.Count == 0)
        {
            logger.LogWarning("No connectors are configured or matched. Output will only appear on stdout.");
        }

        foreach (var repoConfig in targetRepos)
        {
            logger.LogInformation("Processing repository: {Name}", repoConfig.Name);

            try
            {
                // Clone or confirm already cloned
                var repoInfo = gitService.CloneRepository(repoConfig);

                // Pull latest if already cloned
                if (repoInfo.IsCloned)
                    gitService.PullRepository(repoInfo, repoConfig);

                // Compute diff
                var diffResult = from != null
                    ? diffService.GetRefDiff(repoInfo, from, to ?? "HEAD")
                    : diffService.GetTimeWindowDiff(repoInfo, repoConfig.TimeWindow ?? timeWindow);

                logger.LogInformation(
                    "{Name}: {Commits} commits, {Files} files changed (+{Added}/-{Removed})",
                    repoConfig.Name,
                    diffResult.Stats.TotalCommits,
                    diffResult.Stats.TotalFilesChanged,
                    diffResult.Stats.LinesAdded,
                    diffResult.Stats.LinesRemoved);

                // Generate summary
                var message = dryRun
                    ? summaryService.GenerateSimpleSummary(diffResult)
                    : await summaryService.GenerateSummaryAsync(diffResult);

                // Distribute to connectors
                if (dryRun)
                {
                    Console.WriteLine(message.Summary);
                }
                else
                {
                    var tasks = activeConnectors.Select(c => c.SendAsync(message));
                    var results = await Task.WhenAll(tasks);

                    foreach (var result in results)
                    {
                        if (result.Success)
                            logger.LogInformation("Sent to connector '{Connector}'", result.ConnectorName);
                        else
                            logger.LogError("Failed to send to '{Connector}': {Error}", result.ConnectorName, result.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing repository {Name}", repoConfig.Name);
            }
        }
    }
}
