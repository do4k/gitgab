using System.Text;
using System.Text.Json;
using GitGab.Models.Config;
using GitGab.Models.LLM;
using GitGab.Services.Config;
using Microsoft.Extensions.Logging;

namespace GitGab.Services.LLM.Providers;

public class AnthropicProvider : ILLMProvider
{
    private readonly ILogger<AnthropicProvider> _logger;
    private readonly ConfigurationService _configService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public string Name => "anthropic";

    public AnthropicProvider(ILogger<AnthropicProvider> logger, ConfigurationService configService, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configService = configService;
        _httpClientFactory = httpClientFactory;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public async Task<PromptResponse> GenerateAsync(PromptRequest request, CancellationToken ct = default)
    {
        var llmConfig = _configService.GetLLMConfig();
        var apiKey = llmConfig.ApiKey ?? throw new InvalidOperationException("Anthropic API key is not configured");
        var model = request.Model ?? llmConfig.Model;
        var baseUrl = string.IsNullOrEmpty(llmConfig.BaseUrl)
            ? "https://api.anthropic.com"
            : llmConfig.BaseUrl;

        var url = $"{baseUrl.TrimEnd('/')}/v1/messages";

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
            max_tokens = request.MaxTokens,
            temperature = request.Temperature,
            messages = messages
        };

        var jsonBody = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var stringContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient("GitGabHttpClient");
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = stringContent;

        _logger.LogDebug("Sending request to Anthropic API");

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);

        using var jsonDoc = JsonDocument.Parse(responseJson);
        var content = string.Join("\n", jsonDoc.RootElement.GetProperty("content").EnumerateArray().Select(c => c.GetString()));
        var modelUsed = jsonDoc.RootElement.GetProperty("model").GetString() ?? model;

        var usage = jsonDoc.RootElement.GetProperty("usage");
        var inputTokens = usage.GetProperty("input_tokens").GetInt32();
        var outputTokens = usage.GetProperty("output_tokens").GetInt32();

        return new PromptResponse
        {
            Content = content,
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
