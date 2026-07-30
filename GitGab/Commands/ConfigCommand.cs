using System.CommandLine;
using GitGab.Services.Config;
using Microsoft.Extensions.DependencyInjection;

namespace GitGab.Commands;

public class ConfigCommand : Command
{
    public ConfigCommand() : base("config", "Manage configuration")
    {
        AddCommand(new ValidateCommand());
        AddCommand(new ShowCommand());
    }

    private class ValidateCommand : Command
    {
        public ValidateCommand() : base("validate", "Validate configuration")
        {
            this.SetHandler(() =>
            {
                Console.WriteLine("Config validate command - to be implemented");
                return Task.FromResult(0);
            });
        }
    }

    private class ShowCommand : Command
    {
        public ShowCommand() : base("show", "Show current configuration")
        {
            this.SetHandler(() =>
            {
                Console.WriteLine("Config show command - to be implemented");
                return Task.FromResult(0);
            });
        }
    }
}
