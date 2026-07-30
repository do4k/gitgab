using System.CommandLine;
using GitGab.Services.Connector;
using Microsoft.Extensions.DependencyInjection;

namespace GitGab.Commands;

public class ConnectorCommand : Command
{
    private readonly IServiceProvider _services;

    public ConnectorCommand(IServiceProvider services) : base("connector", "Manage connectors")
    {
        _services = services;
        AddCommand(new TestCommand(services));
        AddCommand(new ListCommand(services));
    }

    private class TestCommand : Command
    {
        private readonly IServiceProvider _services;

        public TestCommand(IServiceProvider services) : base("test", "Test a connector")
        {
            _services = services;
            AddOption(new Option<string>(["--name", "-n"], "Connector name to test"));

            this.SetHandler(async (context) =>
            {
                var logger = _services.GetRequiredService<ILogger<TestCommand>>();
                var factory = _services.GetRequiredService<ConnectorFactory>();

                try
                {
                    var connector = factory.CreateConnector(context.Name);
                    logger.LogInformation("Testing connector: {Name}", connector.Name);

                    var testMessage = new ConnectorMessage
                    {
                        RepositoryName = "test-repo",
                        Summary = "This is a test summary",
                        LLMModel = "test-model"
                    };

                    var result = await connector.SendAsync(testMessage, context.CancellationToken);
                    if (result.Success)
                    {
                        logger.LogInformation("Connector test successful");
                        return 0;
                    }
                    else
                    {
                        logger.LogError("Connector test failed: {Error}", result.ErrorMessage);
                        return 1;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error testing connector");
                    return 1;
                }
            });
        }

        private class TestCommandContext
        {
            public string Name { get; set; } = string.Empty;
            public CancellationToken CancellationToken { get; set; }
        }
    }

    private class ListCommand : Command
    {
        private readonly IServiceProvider _services;

        public ListCommand(IServiceProvider services) : base("list", "List configured connectors")
        {
            _services = services;
            this.SetHandler(() =>
            {
                var configService = _services.GetRequiredService<ConfigurationService>();
                var connectors = configService.GetConnectors();
                foreach (var connector in connectors)
                {
                    Console.WriteLine($"- {connector.Name} ({connector.Type})");
                }
                return Task.FromResult(0);
            });
        }
    }
}
