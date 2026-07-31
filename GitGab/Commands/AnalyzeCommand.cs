using System.CommandLine;
using System.Text.Json;
using GitGab.Models.Connector;
using GitGab.Models.Config;
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

        var repoOption = new Option<string?>(
            new[] { "--repo", "-r" },
            "Repository name or URL to analyze (must match a configured repository)");
        var allOption = new Option<bool>(
            new[] { "--all", "-a" },
            "Analyze all configured repositories");
        var timeWindowOption = new Option<string>(
            new[] { "--time-window", "-t" },
            () => "P7D",
            "ISO 8601 duration for the diff window (e.g. P7D, PT6H)");
        var fromOption = new Option<string?>(
            new[] { "--from" },
            "From commit/branch/tag/date");
        var toOption = new Option<string?>(
            new[] { "--to" },
            () => "HEAD",
            "To commit/branch/tag (default: HEAD)");
        var connectorOption = new Option<string[]>(
            new[] { "--connector", "-c" },
            "Connector name(s) to send output to (default: all configured)");
        var dryRunOption = new Option<bool>(
            new[] { "--dry-run" },
            "Generate a plain-text summary without calling the LLM or sending to connectors");
        var outputOption = new Option<string?>(
            new[] { "--output", "-o" },
            "Write the summary to a local file. Use .json extension for JSON output, otherwise markdown.");

        Add(repoOption);
        Add(allOption);
        Add(timeWindowOption);
        Add(fromOption);
        Add(toOption);
        Add(connectorOption);
        Add(dryRunOption);
        Add(outputOption);

        this.SetHandler(async (repo, all, timeWindow, from, to, connectors, dryRun, output) =>
        {
            await RunAsync(repo, all, timeWindow, from, to, connectors, dryRun, output);
        }, repoOption, allOption, timeWindowOption, fromOption, toOption, connectorOption, dryRunOption, outputOption);
    }

    private async Task RunAsync(
        string? repoSpec,
        bool all,
        string timeWindow,
        string? from,
        string? to,
        string[] connectorNames,
        bool dryRun,
        string? outputPath)
    {
        var logger = _services.GetRequiredService<ILogger<AnalyzeCommand>>();
        var configService = _services.GetRequiredService<ConfigurationService>();
        var gitService = _services.GetRequiredService<GitService>();
        var diffService = _services.GetRequiredService<DiffService>();
        var summaryService = _services.GetRequiredService<SummaryService>();
        var connectorFactory = _services.GetRequiredService<ConnectorFactory>();

        var allRepos = configService.GetRepositories();

        if (allRepos.Count == 0)
        {
            logger.LogError("No repositories configured. Add entries under 'Repositories' in appsettings.json.");
            return;
        }

        // Resolve target repos — match by name or URL (case-insensitive)
        List<RepositoryConfig> targetRepos;
        if (all)
        {
            targetRepos = allRepos;
        }
        else if (!string.IsNullOrEmpty(repoSpec))
        {
            targetRepos = allRepos
                .Where(r =>
                    r.Name.Equals(repoSpec, StringComparison.OrdinalIgnoreCase) ||
                    NormalizeUrl(r.Url).Equals(NormalizeUrl(repoSpec), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (targetRepos.Count == 0)
            {
                logger.LogError(
                    "No configured repository matches '{Spec}'. Available repositories:",
                    repoSpec);
                foreach (var r in allRepos)
                    logger.LogError("  • {Name}  ({Url})", r.Name, r.Url);
                return;
            }
        }
        else
        {
            logger.LogError("Specify a repository with --repo <name-or-url>, or use --all.");
            return;
        }

        // Resolve connectors (skipped entirely in dry-run unless --output omitted, handled below)
        var allConnectors = connectorFactory.CreateAllConnectors();
        var activeConnectors = connectorNames.Length > 0
            ? allConnectors.Where(c => connectorNames.Contains(c.Name, StringComparer.OrdinalIgnoreCase)).ToList()
            : allConnectors;

        if (!dryRun && activeConnectors.Count == 0)
            logger.LogWarning("No connectors configured or matched. Output will only appear on stdout.");

        foreach (var repoConfig in targetRepos)
        {
            logger.LogInformation("Processing repository: {Name}", repoConfig.Name);

            try
            {
                var repoInfo = gitService.CloneRepository(repoConfig);
                if (repoInfo.IsCloned)
                    gitService.PullRepository(repoInfo, repoConfig);

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

                var message = dryRun
                    ? summaryService.GenerateSimpleSummary(diffResult)
                    : await summaryService.GenerateSummaryAsync(diffResult);

                // Write to output file if requested
                if (!string.IsNullOrEmpty(outputPath))
                {
                    var resolvedPath = outputPath
                        .Replace("{{repo.name}}", repoConfig.Name)
                        .Replace("{{date}}", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"));

                    await WriteOutputFileAsync(resolvedPath, message, logger);
                }

                // Dry-run: always echo to stdout so the terminal is never blank
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
                            logger.LogInformation("Sent to '{Connector}'", result.ConnectorName);
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Strips trailing .git and normalises the scheme so that
    /// "https://github.com/org/repo.git" and "git@github.com:org/repo" can be
    /// compared after both go through this method.
    /// </summary>
    internal static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        // Normalise SSH shorthand git@host:path -> host/path
        url = url.Trim();
        if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            url = url[4..]; // strip git@
            url = url.Replace(':', '/');
        }

        // Strip scheme (https://, http://, ssh://, etc.)
        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0)
            url = url[(schemeEnd + 3)..];

        // Strip trailing .git
        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            url = url[..^4];

        return url.TrimEnd('/').ToLowerInvariant();
    }

    /// <summary>
    /// Writes the connector message to a file.  If the path ends with .json the
    /// output is serialised as JSON; otherwise the markdown summary is written.
    /// Supports {{repo.name}} and {{date}} placeholders in the path.
    /// </summary>
    private static async Task WriteOutputFileAsync(
        string path,
        ConnectorMessage message,
        ILogger logger)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new
                {
                    repository = message.RepositoryName,
                    generated_at = message.GeneratedAt,
                    llm_model = message.LLMModel,
                    summary = message.Summary,
                    stats = message.DiffResult?.Stats
                };
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(path, json);
            }
            else
            {
                await File.WriteAllTextAsync(path, message.Summary);
            }

            logger.LogInformation("Summary written to {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write output file '{Path}'", path);
        }
    }
}
