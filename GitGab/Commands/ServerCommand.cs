using System.CommandLine;

namespace GitGab.Commands;

public class ServerCommand : Command
{
    public ServerCommand() : base("server", "Run HTTP API server")
    {
        AddOption(new Option<int>(["--port", "-p"], "Server port") { DefaultValue = 8080 });

        this.SetHandler(() =>
        {
            Console.WriteLine("Server command - to be implemented");
            Console.WriteLine("Will run HTTP API on specified port");
            return Task.FromResult(0);
        });
    }
}
