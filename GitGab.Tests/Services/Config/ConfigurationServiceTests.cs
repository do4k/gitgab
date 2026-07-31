using GitGab.Services.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;

namespace GitGab.Tests.Services.Config;

public class ConfigurationServiceTests
{
    private static ConfigurationService BuildService(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new ConfigurationService(config, NullLogger<ConfigurationService>.Instance);
    }

    // ── GetAppSettings ────────────────────────────────────────────────────────

    [Test]
    public async Task GetAppSettings_ReturnsDefaults_WhenSectionMissing()
    {
        var svc = BuildService(new Dictionary<string, string?>());

        var settings = svc.GetAppSettings();

        await Assert.That(settings).IsNotNull();
        // Default values from AppSettings class
        await Assert.That(settings.Name).IsEqualTo("GitGab");
        await Assert.That(settings.RepoCacheDir).IsEqualTo("./cache/repos");
    }

    [Test]
    public async Task GetAppSettings_BindsValuesFromConfiguration()
    {
        var svc = BuildService(new Dictionary<string, string?>
        {
            ["AppSettings:Name"] = "MyGitGab",
            ["AppSettings:RepoCacheDir"] = "/tmp/repos",
            ["AppSettings:MaxConcurrentRepos"] = "10"
        });

        var settings = svc.GetAppSettings();

        await Assert.That(settings.Name).IsEqualTo("MyGitGab");
        await Assert.That(settings.RepoCacheDir).IsEqualTo("/tmp/repos");
        await Assert.That(settings.MaxConcurrentRepos).IsEqualTo(10);
    }

    // ── GetLLMConfig ──────────────────────────────────────────────────────────

    [Test]
    public async Task GetLLMConfig_ReturnsDefaults_WhenSectionMissing()
    {
        var svc = BuildService(new Dictionary<string, string?>());

        var llm = svc.GetLLMConfig();

        await Assert.That(llm.Provider).IsEqualTo("gemini");
        await Assert.That(llm.Model).IsEqualTo("gemini-2.5-flash");
        await Assert.That(llm.Temperature).IsEqualTo(0.3);
    }

    [Test]
    public async Task GetLLMConfig_BindsValuesFromConfiguration()
    {
        var svc = BuildService(new Dictionary<string, string?>
        {
            ["LLM:Provider"] = "openai",
            ["LLM:Model"] = "gpt-4o",
            ["LLM:ApiKey"] = "sk-test",
            ["LLM:MaxTokens"] = "8192"
        });

        var llm = svc.GetLLMConfig();

        await Assert.That(llm.Provider).IsEqualTo("openai");
        await Assert.That(llm.Model).IsEqualTo("gpt-4o");
        await Assert.That(llm.ApiKey).IsEqualTo("sk-test");
        await Assert.That(llm.MaxTokens).IsEqualTo(8192);
    }

    // ── GetPromptConfig ───────────────────────────────────────────────────────

    [Test]
    public async Task GetPromptConfig_BindsTemplate()
    {
        var svc = BuildService(new Dictionary<string, string?>
        {
            ["Prompt:Template"] = "Summarise {{repo.name}}"
        });

        var prompt = svc.GetPromptConfig();

        await Assert.That(prompt.Template).IsEqualTo("Summarise {{repo.name}}");
    }

    // ── GetRepositories ───────────────────────────────────────────────────────

    [Test]
    public async Task GetRepositories_ReturnsEmptyList_WhenSectionMissing()
    {
        var svc = BuildService(new Dictionary<string, string?>());

        var repos = svc.GetRepositories();

        await Assert.That(repos).IsEmpty();
    }

    [Test]
    public async Task GetRepositories_BindsSingleRepository()
    {
        var svc = BuildService(new Dictionary<string, string?>
        {
            ["Repositories:0:Name"] = "my-app",
            ["Repositories:0:Url"] = "https://github.com/org/my-app.git",
            ["Repositories:0:Branch"] = "develop"
        });

        var repos = svc.GetRepositories();

        await Assert.That(repos).Count().IsEqualTo(1);
        await Assert.That(repos[0].Name).IsEqualTo("my-app");
        await Assert.That(repos[0].Url).IsEqualTo("https://github.com/org/my-app.git");
        await Assert.That(repos[0].Branch).IsEqualTo("develop");
    }

    // ── GetConnectors ─────────────────────────────────────────────────────────

    [Test]
    public async Task GetConnectors_ReturnsEmptyList_WhenSectionMissing()
    {
        var svc = BuildService(new Dictionary<string, string?>());

        var connectors = svc.GetConnectors();

        await Assert.That(connectors).IsEmpty();
    }

    [Test]
    public async Task GetConnectors_SkipsEntries_WithMissingTypeOrName()
    {
        var svc = BuildService(new Dictionary<string, string?>
        {
            // Missing Name
            ["Connectors:0:Type"] = "console"
        });

        var connectors = svc.GetConnectors();

        await Assert.That(connectors).IsEmpty();
    }

    [Test]
    public async Task GetConnectors_CreatesSlackConnectorConfig()
    {
        var svc = BuildService(new Dictionary<string, string?>
        {
            ["Connectors:0:Type"] = "slack",
            ["Connectors:0:Name"] = "my-slack",
            ["Connectors:0:WebhookUrl"] = "https://hooks.slack.com/test"
        });

        var connectors = svc.GetConnectors();

        await Assert.That(connectors).Count().IsEqualTo(1);

        var slack = connectors[0] as GitGab.Models.Config.SlackConnectorConfig;
        await Assert.That(slack).IsNotNull();
        await Assert.That(slack!.WebhookUrl).IsEqualTo("https://hooks.slack.com/test");
    }

    [Test]
    public async Task GetConnectors_CreatesConsoleConnectorConfig()
    {
        var svc = BuildService(new Dictionary<string, string?>
        {
            ["Connectors:0:Type"] = "console",
            ["Connectors:0:Name"] = "stdout"
        });

        var connectors = svc.GetConnectors();

        await Assert.That(connectors).Count().IsEqualTo(1);
        await Assert.That(connectors[0]).IsTypeOf<GitGab.Models.Config.ConsoleConnectorConfig>();
    }

    [Test]
    public async Task GetConnectors_CreatesFileConnectorConfig()
    {
        var svc = BuildService(new Dictionary<string, string?>
        {
            ["Connectors:0:Type"] = "file",
            ["Connectors:0:Name"] = "file-out",
            ["Connectors:0:Path"] = "/tmp/out.md"
        });

        var connectors = svc.GetConnectors();

        await Assert.That(connectors).Count().IsEqualTo(1);

        var file = connectors[0] as GitGab.Models.Config.FileConnectorConfig;
        await Assert.That(file).IsNotNull();
        await Assert.That(file!.Path).IsEqualTo("/tmp/out.md");
    }

    // ── GetScheduleConfig ─────────────────────────────────────────────────────

    [Test]
    public async Task GetScheduleConfig_BindsCronExpression()
    {
        var svc = BuildService(new Dictionary<string, string?>
        {
            ["Schedule:CronExpression"] = "0 9 * * 1",
            ["Schedule:Enabled"] = "true"
        });

        var schedule = svc.GetScheduleConfig();

        await Assert.That(schedule.CronExpression).IsEqualTo("0 9 * * 1");
        await Assert.That(schedule.Enabled).IsTrue();
    }
}
