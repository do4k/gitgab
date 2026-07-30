using GitGab.Models.Config;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

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
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        
        var connectorSection = _configuration.GetSection("Connectors");
        var children = connectorSection.GetChildren();
        
        foreach (var child in children)
        {
            var type = child["Type"] ?? string.Empty;
            var json = JsonSerializer.Serialize(child.GetChildren().ToDictionary(x => x.Key, x => x.Value));
            
            ConnectorConfig? connector = type.ToLower() switch
            {
                "slack" => JsonSerializer.Deserialize<SlackConnectorConfig>(json, options),
                "email" => JsonSerializer.Deserialize<EmailConnectorConfig>(json, options),
                "file" => JsonSerializer.Deserialize<FileConnectorConfig>(json, options),
                "webhook" => JsonSerializer.Deserialize<WebhookConnectorConfig>(json, options),
                "console" => JsonSerializer.Deserialize<ConsoleConnectorConfig>(json, options),
                _ => new ConnectorConfig { Type = type, Name = child["Name"] ?? string.Empty }
            };
            
            if (connector != null)
            {
                connectors.Add(connector);
            }
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
