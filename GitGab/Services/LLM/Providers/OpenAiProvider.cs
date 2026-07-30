using GitGab.Models.Config;
using GitGab.Models.LLM;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace GitGab.Services.LLM.Providers;

public class OpenAiProvider : ILLMProvider
{
    private readonly ILogger<OpenAiProvider> _logger;
    private readonly ConfigurationService _configService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public string Name => "openai";

    public OpenAiProvider(ILogger<OpenAiProvider> logger, ConfigurationService configService, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configService = configService;
        _httpClientFactory = httpClientFactory;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public async Task<PromptResponse> GenerateAsync(PromptRequest request, CancellationToken ct = default)
    {
        var llmConfig = _configService.GetLLMConfig();
        var apiKey = llmConfig.ApiKey ?? throw new InvalidOperationException("OpenAI API key is not configured");
        var model = request.Model ?? llmConfig.Model;
        var baseUrl = llmConfig.BaseUrl;

        var url = $"{baseUrl.TrimEnd('/')}/v1/chat/completions";

        // Build messages array
        var messages = new List<object>();
        
        if (!string.IsNullOrEmpty(request.SystemMessage))
        {
            messages.Add(new { role = "system", content = request.SystemMessage });
        }
        
        foreach (var msg in request.Messages)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        var requestBody = new
        {
            model = model,
            messages = messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        };

        var jsonBody = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient("GitGabHttpClient");
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");
        requestMessage.Content = content;

        _logger.LogDebug("Sending request to OpenAI API");

        var response = await httpClient.SendAsync(requestMessage, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);

        using var jsonDoc = JsonDocument.Parse(responseJson);
        var choice = jsonDoc.RootElement.GetProperty("choices")[0];
        var contentText = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
        var modelUsed = jsonDoc.RootElement.GetProperty("model").GetString() ?? model;

        var usage = jsonDoc.RootElement.GetProperty("usage");
        var inputTokens = usage.GetProperty("prompt_tokens").GetInt32();
        var outputTokens = usage.GetProperty("completion_tokens").GetInt32();

        return new PromptResponse
        {
            Content = contentText,
            Model = modelUsed,
            Usage = new UsageInfo
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens
            }
        };
    }
}
