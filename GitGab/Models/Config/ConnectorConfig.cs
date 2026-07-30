using System.Text.Json.Serialization;

namespace GitGab.Models.Config;

[JsonDerivedType(typeof(SlackConnectorConfig), "slack")]
[JsonDerivedType(typeof(EmailConnectorConfig), "email")]
[JsonDerivedType(typeof(FileConnectorConfig), "file")]
[JsonDerivedType(typeof(WebhookConnectorConfig), "webhook")]
[JsonDerivedType(typeof(ConsoleConnectorConfig), "console")]
public abstract class ConnectorConfig
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SlackConnectorConfig : ConnectorConfig
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public string Template { get; set; } = "default";
}

public class EmailConnectorConfig : ConnectorConfig
{
    public SmtpConfig Smtp { get; set; } = new();
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new();
    public string Subject { get; set; } = "GitGab Summary: {{repo.name}}";
    public bool IsHtml { get; set; } = true;
}

public class SmtpConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class FileConnectorConfig : ConnectorConfig
{
    public string Path { get; set; } = string.Empty;
    public string Format { get; set; } = "markdown"; // markdown, json, html
}

public class WebhookConnectorConfig : ConnectorConfig
{
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
    public Dictionary<string, string>? Headers { get; set; }
}

public class ConsoleConnectorConfig : ConnectorConfig
{
    // Just outputs to console, no additional config needed
}

public class ScheduleConfig
{
    public string? CronExpression { get; set; }
    public bool Enabled { get; set; } = false;
}
