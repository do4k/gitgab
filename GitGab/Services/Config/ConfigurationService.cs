using GitGab.Models.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GitGab.Services.Config;

public class ConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(IConfiguration configuration, ILogger<ConfigurationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public AppSettings GetAppSettings()
    {
        var settings = new AppSettings();
        _configuration.GetSection("AppSettings").Bind(settings);
        return settings;
    }

    public List<RepositoryConfig> GetRepositories()
    {
        var repos = new List<RepositoryConfig>();
        _configuration.GetSection("Repositories").Bind(repos);
        return repos;
    }

    public LLMConfig GetLLMConfig()
    {
        var config = new LLMConfig();
        _configuration.GetSection("LLM").Bind(config);
        return config;
    }

    public PromptConfig GetPromptConfig()
    {
        var config = new PromptConfig();
        _configuration.GetSection("Prompt").Bind(config);
        return config;
    }

    public List<ConnectorConfig> GetConnectors()
    {
        var connectors = new List<ConnectorConfig>();
        var connectorSection = _configuration.GetSection("Connectors");
        var children = connectorSection.GetChildren();

        foreach (var child in children)
        {
            var type = child["Type"] ?? string.Empty;
            var name = child["Name"] ?? string.Empty;

            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(name))
                continue;

            // Create type-specific connector configs
            ConnectorConfig connector = type.ToLower() switch
            {
                "slack" => new SlackConnectorConfig { Type = type, Name = name, WebhookUrl = child["WebhookUrl"] ?? "" },
                "email" => new EmailConnectorConfig { Type = type, Name = name, From = child["From"] ?? "" },
                "file" => new FileConnectorConfig { Type = type, Name = name, Path = child["Path"] ?? "" },
                "webhook" => new WebhookConnectorConfig { Type = type, Name = name, Url = child["Url"] ?? "" },
                _ => new ConsoleConnectorConfig { Type = type, Name = name }
            };

            connectors.Add(connector);
        }

        return connectors;
    }

    public ScheduleConfig GetScheduleConfig()
    {
        var config = new ScheduleConfig();
        _configuration.GetSection("Schedule").Bind(config);
        return config;
    }
}
