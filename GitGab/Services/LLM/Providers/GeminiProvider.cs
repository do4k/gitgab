using System.Text;
using System.Text.Json;
using GitGab.Models.Config;
using GitGab.Models.LLM;
using GitGab.Services.Config;
using Microsoft.Extensions.Logging;

namespace GitGab.Services.LLM.Providers;

public class GeminiProvider : ILLMProvider
{
    private readonly ILogger<GeminiProvider> _logger;
    private readonly ConfigurationService _configService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public string Name => "gemini";

    public GeminiProvider(ILogger<GeminiProvider> logger, ConfigurationService configService, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configService = configService;
        _httpClientFactory = httpClientFactory;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public async Task<PromptResponse> GenerateAsync(PromptRequest request, CancellationToken ct = default)
    {
        var llmConfig = _configService.GetLLMConfig();
        var apiKey = llmConfig.ApiKey ?? throw new InvalidOperationException("Gemini API key is not configured");
        var model = request.Model ?? llmConfig.Model;
        var baseUrl = string.IsNullOrEmpty(llmConfig.BaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : llmConfig.BaseUrl;

        var url = $"{baseUrl.TrimEnd('/')}/v1beta/models/{model}:generateContent";

        var promptText = request.SystemMessage + "\n\n" + request.Messages.FirstOrDefault()?.Content;

        var requestBody = new
        {
            contents = new object[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = promptText }
                    }
                }
            },
            generationConfig = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxTokens
            }
        };

        var jsonBody = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var stringContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient("GitGabHttpClient");
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("x-goog-api-key", apiKey);
        httpRequest.Content = stringContent;

        _logger.LogDebug("Sending request to Gemini API");

        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);

        using var jsonDoc = JsonDocument.Parse(responseJson);
        var content = jsonDoc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";

        var usage = jsonDoc.RootElement.GetProperty("usageMetadata");
        var inputTokens = usage.GetProperty("promptTokenCount").GetInt32();
        var outputTokens = usage.GetProperty("candidatesTokenCount").GetInt32();

        return new PromptResponse
        {
            Content = content,
            Model = model,
            Usage = new UsageInfo
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens
            }
        };
    }
}
