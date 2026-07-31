using GitGab.Models.Git;
using GitGab.Services.Summary;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;

namespace GitGab.Tests.Services.Summary;

public class PromptBuilderTests
{
    private readonly PromptBuilder _sut = new(NullLogger<PromptBuilder>.Instance);

    private static DiffResult MakeDiffResult(
        string repoName = "test-repo",
        string from = "2024-01-01",
        string to = "HEAD") =>
        new()
        {
            Repository = new RepositoryInfo { Name = repoName },
            From = from,
            To = to,
            Stats = new GitStats
            {
                TotalCommits = 5,
                TotalFilesChanged = 10,
                LinesAdded = 200,
                LinesRemoved = 50
            },
            Commits = new List<CommitInfo>
            {
                new()
                {
                    Hash = "abc1234567890",
                    Message = "Add feature X",
                    AuthorName = "Alice"
                }
            }
        };

    [Test]
    public async Task BuildPrompt_ReplacesRepoNamePlaceholder()
    {
        var diff = MakeDiffResult("my-repo");
        const string template = "Repo: {{repo.name}}";

        var result = _sut.BuildPrompt(diff, template);

        await Assert.That(result).Contains("my-repo");
        await Assert.That(result).DoesNotContain("{{repo.name}}");
    }

    [Test]
    public async Task BuildPrompt_ReplacesTimeWindowPlaceholder()
    {
        var diff = MakeDiffResult(from: "2024-01-01", to: "2024-01-08");
        const string template = "Period: {{time_window}}";

        var result = _sut.BuildPrompt(diff, template);

        await Assert.That(result).Contains("2024-01-01");
        await Assert.That(result).Contains("2024-01-08");
        await Assert.That(result).DoesNotContain("{{time_window}}");
    }

    [Test]
    public async Task BuildPrompt_ReplacesDiffSummaryPlaceholder()
    {
        var diff = MakeDiffResult();
        const string template = "Changes: {{diff_summary}}";

        var result = _sut.BuildPrompt(diff, template);

        await Assert.That(result).DoesNotContain("{{diff_summary}}");
    }

    [Test]
    public async Task BuildPrompt_DiffSummaryContainsStats()
    {
        var diff = MakeDiffResult();
        diff.Stats.TotalCommits = 42;
        diff.Stats.LinesAdded = 999;

        const string template = "{{diff_summary}}";

        var result = _sut.BuildPrompt(diff, template);

        await Assert.That(result).Contains("42");
        await Assert.That(result).Contains("999");
    }

    [Test]
    public async Task BuildPrompt_DiffSummaryListsCommits()
    {
        var diff = MakeDiffResult();
        diff.Commits =
        [
            new()
            {
                Hash = "aaaaabbbbb111",
                Message = "Fix crash",
                AuthorName = "Bob"
            }
        ];

        const string template = "{{diff_summary}}";

        var result = _sut.BuildPrompt(diff, template);

        // ShortHash is first 7 chars of the hash
        await Assert.That(result).Contains("aaaaabb");
        await Assert.That(result).Contains("Fix crash");
        await Assert.That(result).Contains("Bob");
    }

    [Test]
    public async Task BuildPrompt_DiffSummaryCapsCommitsAtFive()
    {
        var diff = MakeDiffResult();
        diff.Commits = Enumerable.Range(1, 8)
            .Select(i => new CommitInfo
            {
                Hash = $"commit{i:D7}000",
                Message = $"Commit {i}",
                AuthorName = "Dev"
            })
            .ToList();

        const string template = "{{diff_summary}}";

        var result = _sut.BuildPrompt(diff, template);

        await Assert.That(result).Contains("3 more commits");
    }

    [Test]
    public async Task BuildPrompt_DiffSummaryListsFilesByExtension()
    {
        var diff = MakeDiffResult();
        diff.Stats.FilesByExtension = new Dictionary<string, int>
        {
            [".cs"] = 7,
            [".json"] = 2
        };

        const string template = "{{diff_summary}}";

        var result = _sut.BuildPrompt(diff, template);

        await Assert.That(result).Contains(".cs");
        await Assert.That(result).Contains("7");
    }

    [Test]
    public async Task BuildPrompt_EmptyTemplate_ReturnsEmptyString()
    {
        var diff = MakeDiffResult();

        var result = _sut.BuildPrompt(diff, string.Empty);

        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task BuildPrompt_TemplateWithNoPlaceholders_ReturnsTemplateLiteral()
    {
        var diff = MakeDiffResult();
        const string template = "No placeholders here.";

        var result = _sut.BuildPrompt(diff, template);

        await Assert.That(result).IsEqualTo("No placeholders here.");
    }

    [Test]
    public async Task BuildPrompt_AllPlaceholders_AreReplaced()
    {
        var diff = MakeDiffResult("full-test-repo", "2024-01-01", "2024-01-07");
        const string template = "Repo {{repo.name}} from {{time_window}}: {{diff_summary}}";

        var result = _sut.BuildPrompt(diff, template);

        await Assert.That(result).DoesNotContain("{{");
        await Assert.That(result).DoesNotContain("}}");
    }
}
