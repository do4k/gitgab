using GitGab.Models.Config;
using GitGab.Models.LLM;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

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
        var baseUrl = llmConfig.BaseUrl;

        // Build the request URL
        var url = $"{baseUrl.TrimEnd('/')}/v1beta/models/{model}:generateContent";

        // Build request body
        var requestBody = new
        {
            contents = new object[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = request.SystemMessage + "\n\n" + request.Messages.FirstOrDefault()?.Content }
                    }
                }
            },
            generationConfig = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxTokens,
                stopSequences = new string[] { "**" }
            },
            safetySettings = new object[]
            {
                new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_SEXUAL_CONTENT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
            }
        };

        var jsonBody = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var httpClient = _httpClientFactory.CreateClient("GitGabHttpClient");
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Headers.Add("x-goog-api-key", apiKey);
        requestMessage.Content = content;

        _logger.LogDebug("Sending request to Gemini API: {Url}", url);

        var response = await httpClient.SendAsync(requestMessage, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("Gemini response: {Response}", responseJson);

        // Parse response
        using var jsonDoc = JsonDocument.Parse(responseJson);
        var candidates = jsonDoc.RootElement.GetProperty("candidates");
        var content = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";

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
