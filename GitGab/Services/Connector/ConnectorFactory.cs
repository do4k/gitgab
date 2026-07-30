using GitGab.Models.Config;
using GitGab.Services.Connector.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace GitGab.Services.Connector;

public class ConnectorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConfigurationService _configService;

    public ConnectorFactory(IServiceProvider serviceProvider, ConfigurationService configService)
    {
        _serviceProvider = serviceProvider;
        _configService = configService;
    }

    public IConnector CreateConnector(string name)
    {
        var connectors = _configService.GetConnectors();
        var config = connectors.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Connector {name} not found");

        return config.Type.ToLower() switch
        {
            "slack" => new SlackConnector((SlackConnectorConfig)config),
            "email" => new EmailConnector((EmailConnectorConfig)config),
            "file" => new FileConnector((FileConnectorConfig)config),
            "webhook" => new WebhookConnector((WebhookConnectorConfig)config),
            "console" => new ConsoleConnector((ConsoleConnectorConfig)config),
            _ => throw new ArgumentException($"Unknown connector type: {config.Type}")
        };
    }

    public List<IConnector> CreateAllConnectors()
    {
        var connectors = _configService.GetConnectors();
        return connectors.Select(c => CreateConnector(c.Name)).ToList();
    }
}
