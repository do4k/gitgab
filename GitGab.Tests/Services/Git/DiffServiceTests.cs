using GitGab.Models.Git;
using GitGab.Services.Config;
using GitGab.Services.Git;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;

namespace GitGab.Tests.Services.Git;

public class DiffServiceTests
{
    private readonly DiffService _sut;

    public DiffServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:RepoCacheDir"] = Path.GetTempPath()
            })
            .Build();

        var configService = new ConfigurationService(config, NullLogger<ConfigurationService>.Instance);
        var gitService = new GitService(NullLogger<GitService>.Instance, configService);
        _sut = new DiffService(gitService, NullLogger<DiffService>.Instance);
    }

    // ── ParseTimeWindow ──────────────────────────────────────────────────────

    [Test]
    public async Task ParseTimeWindow_NullOrEmpty_ReturnsSevenDays()
    {
        var result = _sut.ParseTimeWindow(string.Empty);

        await Assert.That(result.Days).IsEqualTo(7);
        await Assert.That(result.Hours).IsEqualTo(0);
        await Assert.That(result.Minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_DefaultValue_ReturnsSevenDays()
    {
        var result = _sut.ParseTimeWindow("P7D");

        await Assert.That(result.Days).IsEqualTo(7);
        await Assert.That(result.Hours).IsEqualTo(0);
        await Assert.That(result.Minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_DaysOnly_ParsesCorrectly()
    {
        var result = _sut.ParseTimeWindow("P14D");

        await Assert.That(result.Days).IsEqualTo(14);
        await Assert.That(result.Hours).IsEqualTo(0);
        await Assert.That(result.Minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_HoursOnly_ParsesCorrectly()
    {
        var result = _sut.ParseTimeWindow("PT6H");

        await Assert.That(result.Days).IsEqualTo(0);
        await Assert.That(result.Hours).IsEqualTo(6);
        await Assert.That(result.Minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_MinutesOnly_ParsesCorrectly()
    {
        var result = _sut.ParseTimeWindow("PT30M");

        await Assert.That(result.Days).IsEqualTo(0);
        await Assert.That(result.Hours).IsEqualTo(0);
        await Assert.That(result.Minutes).IsEqualTo(30);
    }

    [Test]
    public async Task ParseTimeWindow_DaysAndHours_ParsesCorrectly()
    {
        var result = _sut.ParseTimeWindow("P1DT12H");

        await Assert.That(result.Days).IsEqualTo(1);
        await Assert.That(result.Hours).IsEqualTo(12);
        await Assert.That(result.Minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_DaysHoursAndMinutes_ParsesCorrectly()
    {
        var result = _sut.ParseTimeWindow("P3DT2H30M");

        await Assert.That(result.Days).IsEqualTo(3);
        await Assert.That(result.Hours).IsEqualTo(2);
        await Assert.That(result.Minutes).IsEqualTo(30);
    }

    [Test]
    public async Task ParseTimeWindow_ZeroDays_ReturnsZeroForAllComponents()
    {
        var result = _sut.ParseTimeWindow("P0D");

        await Assert.That(result.Days).IsEqualTo(0);
        await Assert.That(result.Hours).IsEqualTo(0);
        await Assert.That(result.Minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_LargeDayValue_ParsesCorrectly()
    {
        var result = _sut.ParseTimeWindow("P365D");

        await Assert.That(result.Days).IsEqualTo(365);
        await Assert.That(result.Hours).IsEqualTo(0);
        await Assert.That(result.Minutes).IsEqualTo(0);
    }

    // ── GetTimeWindowDiff (real git repo) ────────────────────────────────────

    [Test]
    public async Task GetTimeWindowDiff_ReturnsStubDiffResult()
    {
        // Walk up from the test assembly location to find the solution root,
        // which is a valid git repository. This avoids a hard-coded path and
        // works both locally and in CI as long as the repo is checked out.
        var solutionRoot = FindSolutionRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Cannot locate .git directory relative to test output.");

        var repoInfo = new RepositoryInfo
        {
            Name = "GitGab",
            LocalPath = solutionRoot,
            IsCloned = true
        };

        var result = _sut.GetTimeWindowDiff(repoInfo, "P7D");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Repository).IsEqualTo(repoInfo);
    }

    /// <summary>Walks up the directory tree until it finds a .git folder.</summary>
    private static string? FindSolutionRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
