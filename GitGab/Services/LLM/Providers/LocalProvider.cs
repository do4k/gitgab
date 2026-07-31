using System.Text;
using System.Text.Json;
using GitGab.Models.Config;
using GitGab.Models.LLM;
using GitGab.Services.Config;
using Microsoft.Extensions.Logging;

namespace GitGab.Services.LLM.Providers;

public class LocalProvider : ILLMProvider
{
    private readonly ILogger<LocalProvider> _logger;
    private readonly ConfigurationService _configService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public string Name => "local";

    public LocalProvider(ILogger<LocalProvider> logger, ConfigurationService configService, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configService = configService;
        _httpClientFactory = httpClientFactory;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public async Task<PromptResponse> GenerateAsync(PromptRequest request, CancellationToken ct = default)
    {
        var llmConfig = _configService.GetLLMConfig();
        var model = request.Model ?? llmConfig.Model;
        var baseUrl = llmConfig.BaseUrl;

        if (string.IsNullOrEmpty(baseUrl))
        {
            throw new InvalidOperationException("Local LLM base URL is not configured");
        }

        var url = $"{baseUrl.TrimEnd('/')}/v1/chat/completions";

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
            max_tokens = request.MaxTokens,
            stream = false
        };

        var jsonBody = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var stringContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient("GitGabHttpClient");
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = stringContent;

        _logger.LogDebug("Sending request to local LLM API at {Url}", url);

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);

        using var jsonDoc = JsonDocument.Parse(responseJson);
        var content = jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        var modelUsed = jsonDoc.RootElement.GetProperty("model").GetString() ?? model;

        var usage = jsonDoc.RootElement.GetProperty("usage");
        var inputTokens = usage.GetProperty("prompt_tokens").GetInt32();
        var outputTokens = usage.GetProperty("completion_tokens").GetInt32();

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
