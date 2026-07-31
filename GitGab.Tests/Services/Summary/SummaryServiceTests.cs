using GitGab.Models.Git;
using GitGab.Services.Config;
using GitGab.Services.Git;
using GitGab.Services.LLM;
using GitGab.Services.Summary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;

namespace GitGab.Tests.Services.Summary;

public class SummaryServiceTests
{
    private readonly SummaryService _sut;

    public SummaryServiceTests()
    {
        // Wire up the real dependency chain — GenerateSimpleSummary never calls the
        // LLM provider, so we only need the prompt builder + config service.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:RepoCacheDir"] = Path.GetTempPath(),
                ["LLM:Provider"] = "gemini",
                ["LLM:Model"] = "gemini-2.5-flash",
                ["Prompt:Template"] = "Repo: {{repo.name}}"
            })
            .Build();

        var configService = new ConfigurationService(config, NullLogger<ConfigurationService>.Instance);
        var gitService = new GitService(NullLogger<GitService>.Instance, configService);
        var promptBuilder = new PromptBuilder(NullLogger<PromptBuilder>.Instance);

        // LLMProviderFactory needs a service provider; it is never invoked by GenerateSimpleSummary.
        // We pass a stub service provider using a simple lambda-based stand-in.
        var sp = new StubServiceProvider();
        var factory = new LLMProviderFactory(sp, NullLogger<LLMProviderFactory>.Instance, configService);

        _sut = new SummaryService(factory, promptBuilder, configService, NullLogger<SummaryService>.Instance);
    }

    private static DiffResult MakeDiffResult(string repoName = "acme-api") =>
        new()
        {
            Repository = new RepositoryInfo { Name = repoName },
            From = "2024-01-01",
            To = "HEAD",
            Stats = new GitStats
            {
                TotalCommits = 3,
                TotalFilesChanged = 6,
                LinesAdded = 100,
                LinesRemoved = 20
            },
            Commits = new List<CommitInfo>
            {
                new() { Hash = "abc1234567890", Message = "Add login flow", AuthorName = "Alice" },
                new() { Hash = "def1234567890", Message = "Fix null ref",   AuthorName = "Bob" },
                new() { Hash = "ghi1234567890", Message = "Update deps",    AuthorName = "Alice" }
            }
        };

    // ── GenerateSimpleSummary ─────────────────────────────────────────────────

    [Test]
    public async Task GenerateSimpleSummary_SetsRepositoryName()
    {
        var result = _sut.GenerateSimpleSummary(MakeDiffResult("my-service"));

        await Assert.That(result.RepositoryName).IsEqualTo("my-service");
    }

    [Test]
    public async Task GenerateSimpleSummary_SummaryContainsRepoName()
    {
        var result = _sut.GenerateSimpleSummary(MakeDiffResult("my-service"));

        await Assert.That(result.Summary).Contains("my-service");
    }

    [Test]
    public async Task GenerateSimpleSummary_SummaryContainsCommitCount()
    {
        var result = _sut.GenerateSimpleSummary(MakeDiffResult());

        await Assert.That(result.Summary).Contains("3"); // TotalCommits
    }

    [Test]
    public async Task GenerateSimpleSummary_SummaryContainsFilesChanged()
    {
        var result = _sut.GenerateSimpleSummary(MakeDiffResult());

        await Assert.That(result.Summary).Contains("6"); // TotalFilesChanged
    }

    [Test]
    public async Task GenerateSimpleSummary_SummaryContainsLinesAdded()
    {
        var result = _sut.GenerateSimpleSummary(MakeDiffResult());

        await Assert.That(result.Summary).Contains("100"); // LinesAdded
    }

    [Test]
    public async Task GenerateSimpleSummary_SummaryContainsLinesRemoved()
    {
        var result = _sut.GenerateSimpleSummary(MakeDiffResult());

        await Assert.That(result.Summary).Contains("20"); // LinesRemoved
    }

    [Test]
    public async Task GenerateSimpleSummary_SummaryListsAllCommits()
    {
        var result = _sut.GenerateSimpleSummary(MakeDiffResult());

        await Assert.That(result.Summary).Contains("Add login flow");
        await Assert.That(result.Summary).Contains("Fix null ref");
        await Assert.That(result.Summary).Contains("Update deps");
    }

    [Test]
    public async Task GenerateSimpleSummary_LLMModelIndicatesDryRun()
    {
        var result = _sut.GenerateSimpleSummary(MakeDiffResult());

        await Assert.That(result.LLMModel).Contains("none");
    }

    [Test]
    public async Task GenerateSimpleSummary_EmptyCommitList_StillReturnsValidSummary()
    {
        var diff = MakeDiffResult();
        diff.Commits.Clear();

        var result = _sut.GenerateSimpleSummary(diff);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Summary).IsNotNull();
    }

    [Test]
    public async Task GenerateSimpleSummary_MoreThanTenCommits_OnlyShowsFirstTen()
    {
        var diff = MakeDiffResult();
        diff.Commits = Enumerable.Range(1, 15)
            .Select(i => new CommitInfo
            {
                Hash = $"hash{i:D8}000",
                Message = $"Commit message {i}",
                AuthorName = "Dev"
            })
            .ToList();

        var result = _sut.GenerateSimpleSummary(diff);

        // Items 11–15 should be truncated by the Take(10) in the implementation
        await Assert.That(result.Summary).DoesNotContain("Commit message 11");
        await Assert.That(result.Summary).DoesNotContain("Commit message 15");
        await Assert.That(result.Summary).Contains("Commit message 10");
    }

    // ── Inner helpers ─────────────────────────────────────────────────────────

    /// <summary>Minimal IServiceProvider stub — GetService always returns null.</summary>
    private sealed class StubServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
