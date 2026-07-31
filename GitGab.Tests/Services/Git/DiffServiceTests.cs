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
        var (days, hours, minutes) = _sut.ParseTimeWindow(string.Empty);

        await Assert.That(days).IsEqualTo(7);
        await Assert.That(hours).IsEqualTo(0);
        await Assert.That(minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_DefaultValue_ReturnsSevenDays()
    {
        var (days, hours, minutes) = _sut.ParseTimeWindow("P7D");

        await Assert.That(days).IsEqualTo(7);
        await Assert.That(hours).IsEqualTo(0);
        await Assert.That(minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_DaysOnly_ParsesCorrectly()
    {
        var (days, hours, minutes) = _sut.ParseTimeWindow("P14D");

        await Assert.That(days).IsEqualTo(14);
        await Assert.That(hours).IsEqualTo(0);
        await Assert.That(minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_HoursOnly_ParsesCorrectly()
    {
        var (days, hours, minutes) = _sut.ParseTimeWindow("PT6H");

        await Assert.That(days).IsEqualTo(0);
        await Assert.That(hours).IsEqualTo(6);
        await Assert.That(minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_MinutesOnly_ParsesCorrectly()
    {
        var (days, hours, minutes) = _sut.ParseTimeWindow("PT30M");

        await Assert.That(days).IsEqualTo(0);
        await Assert.That(hours).IsEqualTo(0);
        await Assert.That(minutes).IsEqualTo(30);
    }

    [Test]
    public async Task ParseTimeWindow_DaysAndHours_ParsesCorrectly()
    {
        var (days, hours, minutes) = _sut.ParseTimeWindow("P1DT12H");

        await Assert.That(days).IsEqualTo(1);
        await Assert.That(hours).IsEqualTo(12);
        await Assert.That(minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_DaysHoursAndMinutes_ParsesCorrectly()
    {
        var (days, hours, minutes) = _sut.ParseTimeWindow("P3DT2H30M");

        await Assert.That(days).IsEqualTo(3);
        await Assert.That(hours).IsEqualTo(2);
        await Assert.That(minutes).IsEqualTo(30);
    }

    [Test]
    public async Task ParseTimeWindow_ZeroDays_ReturnsZeroForAllComponents()
    {
        var (days, hours, minutes) = _sut.ParseTimeWindow("P0D");

        await Assert.That(days).IsEqualTo(0);
        await Assert.That(hours).IsEqualTo(0);
        await Assert.That(minutes).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTimeWindow_LargeDayValue_ParsesCorrectly()
    {
        var (days, hours, minutes) = _sut.ParseTimeWindow("P365D");

        await Assert.That(days).IsEqualTo(365);
        await Assert.That(hours).IsEqualTo(0);
        await Assert.That(minutes).IsEqualTo(0);
    }

    // ── GetTimeWindowDiff (stub backend) ─────────────────────────────────────

    [Test]
    public async Task GetTimeWindowDiff_ReturnsStubDiffResult()
    {
        var repoInfo = new RepositoryInfo
        {
            Name = "test-repo",
            LocalPath = Path.GetTempPath(),
            IsCloned = true
        };

        var result = _sut.GetTimeWindowDiff(repoInfo, "P7D");

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Repository).IsEqualTo(repoInfo);
    }
}
