using GitGab.Models.Config;
using GitGab.Services.Config;
using GitGab.Services.LLM.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GitGab.Services.LLM;

public class LLMProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LLMProviderFactory> _logger;
    private readonly ConfigurationService _configService;

    public LLMProviderFactory(IServiceProvider serviceProvider, ILogger<LLMProviderFactory> logger, ConfigurationService configService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configService = configService;
    }

    public ILLMProvider CreateProvider(string? providerName = null)
    {
        var llmConfig = _configService.GetLLMConfig();
        var provider = providerName ?? llmConfig.Provider;

        return provider.ToLower() switch
        {
            "gemini" or "google" => ActivatorUtilities.CreateInstance<GeminiProvider>(_serviceProvider),
            "openai" => ActivatorUtilities.CreateInstance<OpenAiProvider>(_serviceProvider),
            "anthropic" => ActivatorUtilities.CreateInstance<AnthropicProvider>(_serviceProvider),
            "local" => ActivatorUtilities.CreateInstance<LocalProvider>(_serviceProvider),
            _ => throw new ArgumentException($"Unknown LLM provider: {provider}")
        };
    }
}
