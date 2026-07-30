using System.CommandLine;
using GitGab.Models.Connector;
using GitGab.Services.Config;
using GitGab.Services.Connector;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GitGab.Commands;

public class ConnectorCommand : Command
{
    private readonly IServiceProvider _services;

    public ConnectorCommand(IServiceProvider services) : base("connector", "Manage connectors")
    {
        _services = services;
        Add(new TestCommand(services));
        Add(new ListCommand(services));
    }

    private class TestCommand : Command
    {
        private readonly IServiceProvider _services;

        public TestCommand(IServiceProvider services) : base("test", "Test a connector")
        {
            _services = services;
            var nameOption = new Option<string?>(new[] { "--name", "-n" }, "Connector name to test");
            Add(nameOption);

            this.SetHandler(() =>
            {
                Console.WriteLine("Connector test command - to be implemented");
            });
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
            });
        }
    }
}
