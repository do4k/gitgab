using GitGab.Models.Config;
using GitGab.Models.Connector;
using System.Text;
using System.Text.Json;

namespace GitGab.Services.Connector.Providers;

public class WebhookConnector : IConnector
{
    private readonly WebhookConnectorConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public string Name => _config.Name;

    public WebhookConnector(WebhookConnectorConfig config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ConnectorResult> SendAsync(ConnectorMessage message, CancellationToken ct = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("GitGabHttpClient");
            var request = new HttpRequestMessage(new HttpMethod(_config.Method), _config.Url);

            // Add custom headers
            if (_config.Headers != null)
            {
                foreach (var header in _config.Headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            // Create payload
            var payload = new
            {
                repository = message.RepositoryName,
                summary = message.Summary,
                generatedAt = message.GeneratedAt.ToString("o"),
                model = message.LLMModel,
                stats = message.DiffResult?.Stats
            };

            var json = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            return new ConnectorResult
            {
                ConnectorName = Name,
                Success = true,
                SentAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new ConnectorResult
            {
                ConnectorName = Name,
                Success = false,
                ErrorMessage = ex.Message,
                SentAt = DateTimeOffset.UtcNow
            };
        }
    }
}
