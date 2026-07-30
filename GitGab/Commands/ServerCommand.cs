using System.CommandLine;

namespace GitGab.Commands;

public class ServerCommand : Command
{
    public ServerCommand() : base("server", "Run HTTP API server")
    {
        var portOption = new Option<int>(new[] { "--port", "-p" }, () => 8080, "Server port");
        Add(portOption);

        this.SetHandler(() =>
        {
            Console.WriteLine("Server command - to be implemented");
        });
    }
}
