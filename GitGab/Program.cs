using System.CommandLine;
using GitGab.Commands;
using GitGab.Services.Config;
using GitGab.Services.Connector;
using GitGab.Services.Git;
using GitGab.Services.LLM;
using GitGab.Services.Summary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GitGab;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        var rootCommand = new RootCommand("GitGab - AI-Powered Repository Change Summarizer");
        
        // Register commands
        rootCommand.AddCommand(new AnalyzeCommand(host.Services));
        rootCommand.AddCommand(new RepoCommand());
        rootCommand.AddCommand(new ConnectorCommand(host.Services));
        rootCommand.AddCommand(new ConfigCommand());
        rootCommand.AddCommand(new ServerCommand());
        
        return await rootCommand.InvokeAsync(args);
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile(
                    $"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                    optional: true,
                    reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<AppSettings>(context.Configuration.GetSection("AppSettings"));
                
                // Services
                services.AddSingleton<IConfiguration>(context.Configuration);
                services.AddSingleton<ConfigurationService>();
                services.AddSingleton<GitService>();
                services.AddSingleton<DiffService>();
                services.AddSingleton<LLMProviderFactory>();
                services.AddSingleton<SummaryService>();
                services.AddSingleton<PromptBuilder>();
                services.AddSingleton<ConnectorFactory>();
                
                // HTTP Client with Polly retries
                services.AddHttpClient("GitGabHttpClient")
                    .AddTransientHttpErrorPolicy(policy => 
                        policy.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));
            });
    }
}
