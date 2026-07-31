using GitGab.Models.Config;
using GitGab.Models.Connector;
using GitGab.Services.Config;
using GitGab.Services.Connector;
using GitGab.Services.Connector.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TUnit.Core;

namespace GitGab.Tests.Services.Connector;

public class ConnectorFactoryTests
{
    private static ConfigurationService BuildConfigService(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new ConfigurationService(config, NullLogger<ConfigurationService>.Instance);
    }

    private static IHttpClientFactory FakeHttpClientFactory()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());
        return factory;
    }

    private static IServiceProvider BuildServiceProvider(IHttpClientFactory httpClientFactory)
    {
        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IHttpClientFactory)).Returns(httpClientFactory);
        return sp;
    }

    [Test]
    public async Task CreateConnector_Slack_ReturnsSlackConnector()
    {
        var configService = BuildConfigService(new Dictionary<string, string?>
        {
            ["Connectors:0:Type"] = "slack",
            ["Connectors:0:Name"] = "my-slack",
            ["Connectors:0:WebhookUrl"] = "https://hooks.slack.com/test"
        });
        var httpFactory = FakeHttpClientFactory();
        var factory = new ConnectorFactory(BuildServiceProvider(httpFactory), configService);

        var connector = factory.CreateConnector("my-slack");

        await Assert.That(connector).IsTypeOf<SlackConnector>();
        await Assert.That(connector.Name).IsEqualTo("my-slack");
    }

    [Test]
    public async Task CreateConnector_Console_ReturnsConsoleConnector()
    {
        var configService = BuildConfigService(new Dictionary<string, string?>
        {
            ["Connectors:0:Type"] = "console",
            ["Connectors:0:Name"] = "stdout"
        });
        var factory = new ConnectorFactory(BuildServiceProvider(FakeHttpClientFactory()), configService);

        var connector = factory.CreateConnector("stdout");

        await Assert.That(connector).IsTypeOf<ConsoleConnector>();
    }

    [Test]
    public async Task CreateConnector_File_ReturnsFileConnector()
    {
        var configService = BuildConfigService(new Dictionary<string, string?>
        {
            ["Connectors:0:Type"] = "file",
            ["Connectors:0:Name"] = "file-out",
            ["Connectors:0:Path"] = "/tmp/out.md"
        });
        var factory = new ConnectorFactory(BuildServiceProvider(FakeHttpClientFactory()), configService);

        var connector = factory.CreateConnector("file-out");

        await Assert.That(connector).IsTypeOf<FileConnector>();
    }

    [Test]
    public async Task CreateConnector_Webhook_ReturnsWebhookConnector()
    {
        var configService = BuildConfigService(new Dictionary<string, string?>
        {
            ["Connectors:0:Type"] = "webhook",
            ["Connectors:0:Name"] = "my-hook",
            ["Connectors:0:Url"] = "https://example.com/hook"
        });
        var factory = new ConnectorFactory(BuildServiceProvider(FakeHttpClientFactory()), configService);

        var connector = factory.CreateConnector("my-hook");

        await Assert.That(connector).IsTypeOf<WebhookConnector>();
    }

    [Test]
    public async Task CreateConnector_UnknownName_ThrowsArgumentException()
    {
        var configService = BuildConfigService(new Dictionary<string, string?>());
        var factory = new ConnectorFactory(BuildServiceProvider(FakeHttpClientFactory()), configService);

        await Assert.That(async () => factory.CreateConnector("not-found"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task CreateAllConnectors_ReturnsAllConfiguredConnectors()
    {
        var configService = BuildConfigService(new Dictionary<string, string?>
        {
            ["Connectors:0:Type"] = "console",
            ["Connectors:0:Name"] = "out1",
            ["Connectors:1:Type"] = "console",
            ["Connectors:1:Name"] = "out2"
        });
        var factory = new ConnectorFactory(BuildServiceProvider(FakeHttpClientFactory()), configService);

        var all = factory.CreateAllConnectors();

        await Assert.That(all).Count().IsEqualTo(2);
    }
}

public class ConsoleConnectorTests
{
    private static ConnectorMessage MakeMessage(string repo = "test-repo") =>
        new()
        {
            RepositoryName = repo,
            Summary = "Some summary text",
            LLMModel = "none"
        };

    [Test]
    public async Task SendAsync_ReturnsSuccess()
    {
        var connector = new ConsoleConnector(new ConsoleConnectorConfig { Name = "stdout" });

        var result = await connector.SendAsync(MakeMessage());

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConnectorName).IsEqualTo("stdout");
    }

    [Test]
    public async Task SendAsync_NeverThrows()
    {
        var connector = new ConsoleConnector(new ConsoleConnectorConfig { Name = "stdout" });

        // Should not throw even with empty summary
        await Assert.That(async () =>
        {
            _ = await connector.SendAsync(new ConnectorMessage
            {
                RepositoryName = "repo",
                Summary = "",
                LLMModel = "none"
            });
        }).ThrowsNothing();
    }
}

public class FileConnectorTests
{
    [Test]
    public async Task SendAsync_WritesMarkdownFile_AndReturnsSuccess()
    {
        var tmpPath = Path.Combine(Path.GetTempPath(), $"gitgab-test-{Guid.NewGuid()}.md");

        try
        {
            var connector = new FileConnector(new FileConnectorConfig
            {
                Name = "file-out",
                Path = tmpPath,
                Format = "markdown"
            });

            var message = new ConnectorMessage
            {
                RepositoryName = "test-repo",
                Summary = "# Test Summary\nSome content here.",
                LLMModel = "none"
            };

            var result = await connector.SendAsync(message);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(tmpPath)).IsTrue();

            var content = await File.ReadAllTextAsync(tmpPath);
            await Assert.That(content).Contains("Test Summary");
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }

    [Test]
    public async Task SendAsync_ReplacesPlaceholdersInPath()
    {
        var tmpDir = Path.GetTempPath();
        var pathTemplate = Path.Combine(tmpDir, "gitgab-{{repo.name}}-test.md");

        var connector = new FileConnector(new FileConnectorConfig
        {
            Name = "file-out",
            Path = pathTemplate,
            Format = "markdown"
        });

        var message = new ConnectorMessage
        {
            RepositoryName = "my-repo",
            Summary = "Content",
            LLMModel = "none"
        };

        var result = await connector.SendAsync(message);
        var expectedPath = Path.Combine(tmpDir, $"gitgab-my-repo-test.md");

        try
        {
            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(expectedPath)).IsTrue();
        }
        finally
        {
            if (File.Exists(expectedPath)) File.Delete(expectedPath);
        }
    }

    [Test]
    public async Task SendAsync_JsonFormat_WritesValidJson()
    {
        var tmpPath = Path.Combine(Path.GetTempPath(), $"gitgab-test-{Guid.NewGuid()}.json");

        try
        {
            var connector = new FileConnector(new FileConnectorConfig
            {
                Name = "file-out",
                Path = tmpPath,
                Format = "json"
            });

            var message = new ConnectorMessage
            {
                RepositoryName = "json-repo",
                Summary = "Summary content",
                LLMModel = "none"
            };

            var result = await connector.SendAsync(message);

            await Assert.That(result.Success).IsTrue();
            var json = await File.ReadAllTextAsync(tmpPath);
            // Should be valid JSON
            var parsed = System.Text.Json.JsonDocument.Parse(json);
            await Assert.That(parsed).IsNotNull();
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }

    [Test]
    public async Task SendAsync_InvalidPath_ReturnsFailure()
    {
        var connector = new FileConnector(new FileConnectorConfig
        {
            Name = "file-out",
            // Null bytes make any OS reject the path
            Path = "/definitely/does/not/exist/\0/out.md",
            Format = "markdown"
        });

        var result = await connector.SendAsync(new ConnectorMessage
        {
            RepositoryName = "repo",
            Summary = "x",
            LLMModel = "none"
        });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsNotNull();
    }
}
