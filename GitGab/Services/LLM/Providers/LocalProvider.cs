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
            throw new InvalidOperationException(
                "Local LLM base URL is not configured. Set LLM:BaseUrl in appsettings (e.g. http://localhost:11434).");
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

        // Local models can be slow on first token — apply the configured timeout.
        httpClient.Timeout = TimeSpan.FromSeconds(llmConfig.TimeoutSeconds > 0 ? llmConfig.TimeoutSeconds : 120);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = stringContent;

        _logger.LogDebug("Sending request to local LLM at {Url} (model: {Model}, timeout: {Timeout}s)",
            url, model, httpClient.Timeout.TotalSeconds);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(httpRequest, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Local LLM at {url} did not respond within {httpClient.Timeout.TotalSeconds}s. " +
                $"Increase LLM:TimeoutSeconds in appsettings if the model needs longer to load.");
        }

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);

        using var jsonDoc = JsonDocument.Parse(responseJson);
        var root = jsonDoc.RootElement;

        var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        var modelUsed = root.TryGetProperty("model", out var modelProp) ? (modelProp.GetString() ?? model) : model;

        var inputTokens = 0;
        var outputTokens = 0;
        if (root.TryGetProperty("usage", out var usageProp))
        {
            if (usageProp.TryGetProperty("prompt_tokens", out var pt)) inputTokens = pt.GetInt32();
            if (usageProp.TryGetProperty("completion_tokens", out var ct2)) outputTokens = ct2.GetInt32();
        }

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
