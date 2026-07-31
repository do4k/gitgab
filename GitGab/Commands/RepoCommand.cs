using System.CommandLine;

namespace GitGab.Commands;

public class RepoCommand : Command
{
    public RepoCommand() : base("repo", "Manage repositories")
    {
        Add(new ListCommand());
        Add(new AddRepoCommand());
        Add(new RemoveCommand());
    }

    private class ListCommand : Command
    {
        public ListCommand() : base("list", "List configured repositories")
        {
            this.SetHandler(() =>
            {
                Console.WriteLine("Repo list command - to be implemented");
            });
        }
    }

    private class AddRepoCommand : Command
    {
        public AddRepoCommand() : base("add", "Add a new repository")
        {
            var nameOption = new Option<string>(new[] { "--name", "-n" }, "Repository name");
            var urlOption = new Option<string>(new[] { "--url", "-u" }, "Repository URL");
            var branchOption = new Option<string>(new[] { "--branch", "-b" }, () => "main", "Branch to track");
            var authTypeOption = new Option<string>(new[] { "--auth-type" }, () => "https", "Authentication type");
            var tokenOption = new Option<string>(new[] { "--token" }, "Access token");

            this.Add(nameOption);
            this.Add(urlOption);
            this.Add(branchOption);
            this.Add(authTypeOption);
            this.Add(tokenOption);

            this.SetHandler(() =>
            {
                Console.WriteLine("Repo add command - to be implemented");
            });
        }
    }

    private class RemoveCommand : Command
    {
        public RemoveCommand() : base("remove", "Remove a repository")
        {
            var nameOption = new Option<string>(new[] { "--name", "-n" }, "Repository name");
            this.Add(nameOption);

            this.SetHandler(() =>
            {
                Console.WriteLine("Repo remove command - to be implemented");
            });
        }
    }
}
