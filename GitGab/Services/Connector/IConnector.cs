using GitGab.Models.Connector;

namespace GitGab.Services.Connector;

public interface IConnector
{
    string Name { get; }
    Task<ConnectorResult> SendAsync(ConnectorMessage message, CancellationToken ct = default);
}
