using System.CommandLine;

namespace GitGab.Commands;

public class RepoCommand : Command
{
    public RepoCommand() : base("repo", "Manage repositories")
    {
        AddCommand(new ListCommand());
        AddCommand(new AddCommand());
        AddCommand(new RemoveCommand());
    }

    private class ListCommand : Command
    {
        public ListCommand() : base("list", "List configured repositories")
        {
            this.SetHandler(() =>
            {
                Console.WriteLine("Repo list command - to be implemented");
                return Task.FromResult(0);
            });
        }
    }

    private class AddCommand : Command
    {
        public AddCommand() : base("add", "Add a new repository")
        {
            AddOption(new Option<string>(["--name", "-n"], "Repository name"));
            AddOption(new Option<string>(["--url", "-u"], "Repository URL"));
            AddOption(new Option<string>(["--branch", "-b"], "Branch to track") { DefaultValue = "main" });
            AddOption(new Option<string>(["--auth-type"], "Authentication type (https/ssh)") { DefaultValue = "https" });
            AddOption(new Option<string>(["--token"], "Access token"));

            this.SetHandler(() =>
            {
                Console.WriteLine("Repo add command - to be implemented");
                return Task.FromResult(0);
            });
        }
    }

    private class RemoveCommand : Command
    {
        public RemoveCommand() : base("remove", "Remove a repository")
        {
            AddOption(new Option<string>(["--name", "-n"], "Repository name"));

            this.SetHandler(() =>
            {
                Console.WriteLine("Repo remove command - to be implemented");
                return Task.FromResult(0);
            });
        }
    }
}
