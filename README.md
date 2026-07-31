# GitGab

**GitGab** is a .NET 10 CLI tool that monitors git repositories, computes diffs over configurable time windows, and sends AI-generated change summaries to Slack, email, files, webhooks, or the console.

```
gitgab analyze --all --dry-run
```

---

## Features

- **Multi-repo**: monitor any number of repositories in one run
- **Flexible diff windows**: ISO 8601 durations (`P7D`, `PT6H`, `P1DT12H`) or explicit ref ranges (`--from v1.0.0 --to v1.1.0`)
- **LLM providers**: Gemini, OpenAI, Anthropic, or any local Ollama-compatible endpoint
- **Connectors**: Slack, email (SMTP), file output, generic webhook, console
- **Dry-run mode**: generates a plain-text summary locally without calling any LLM or sending to connectors
- **Config-driven**: everything lives in `appsettings.json` + environment variable overrides

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git (for cloning repos)
- An API key for your chosen LLM provider (optional for `--dry-run`)

---

## Quick Start

```bash
# Clone
git clone https://github.com/do4k/gitgab.git
cd gitgab

# Build
dotnet build

# Dry-run against all configured repositories
dotnet run --project GitGab -- analyze --all --dry-run

# Run against a single repo with a live LLM
export GEMINI_API_KEY=your-key-here
dotnet run --project GitGab -- analyze --repo my-app --time-window P7D
```

---

## Configuration

GitGab reads `appsettings.json` (committed defaults) and merges `appsettings.Development.json` (git-ignored, for local secrets). All string values support `${ENV_VAR}` token substitution.

### Minimal `appsettings.json`

```json
{
  "AppSettings": {
    "RepoCacheDir": "./cache/repos"
  },
  "Repositories": [
    {
      "Name": "my-app",
      "Url": "https://github.com/org/my-app.git",
      "Branch": "main",
      "Auth": {
        "Type": "https",
        "Token": "${GITHUB_TOKEN}"
      },
      "TimeWindow": "P7D"
    }
  ],
  "LLM": {
    "Provider": "gemini",
    "Model": "gemini-2.5-flash",
    "ApiKey": "${GEMINI_API_KEY}",
    "Temperature": 0.3,
    "MaxTokens": 4096
  },
  "Prompt": {
    "Template": "Summarise changes in {{repo.name}} for {{time_window}}:\n\n{{diff_summary}}"
  },
  "Connectors": [
    {
      "Type": "console",
      "Name": "console-output"
    }
  ]
}
```

### Authentication

| Auth type | Config fields |
|---|---|
| HTTPS token (GitHub / GitLab) | `Auth.Type = "https"`, `Auth.Token` |
| HTTPS username + password | `Auth.Type = "https"`, `Auth.Username`, `Auth.Password` |
| SSH (via system ssh-agent) | `Auth.Type = "ssh"` |

### LLM Providers

| `Provider` value | Notes |
|---|---|
| `gemini` / `google` | Requires `GEMINI_API_KEY` |
| `openai` | Requires `OPENAI_API_KEY`; set `LLM.BaseUrl` for Azure or compatible endpoints |
| `anthropic` | Requires `ANTHROPIC_API_KEY` |
| `local` | Ollama-style OpenAI-compatible endpoint; set `LLM.BaseUrl` (e.g. `http://localhost:11434`) |

### Connectors

| `Type` | Required fields |
|---|---|
| `console` | — |
| `file` | `Path` (supports `{{repo.name}}` and `{{date}}`); `Format`: `markdown` or `json` |
| `slack` | `WebhookUrl` |
| `email` | `Smtp.Host`, `Smtp.Port`, `From`, `To[]`, `Subject` |
| `webhook` | `Url`, optional `Headers` |

---

## CLI Reference

```
gitgab analyze [options]   Analyze repositories and generate summaries
gitgab repo list           List configured repositories
gitgab repo add            Add a repository (stub)
gitgab repo remove         Remove a repository (stub)
gitgab connector test      Send a test message via a connector
gitgab config validate     Validate appsettings.json (stub)
gitgab server              HTTP API server mode (not yet implemented)
```

### `analyze` options

| Option | Default | Description |
|---|---|---|
| `--repo`, `-r` | — | Name of a single repository to analyse |
| `--all`, `-a` | false | Analyse all configured repositories |
| `--time-window`, `-t` | `P7D` | ISO 8601 duration (overridden per-repo by config) |
| `--from` | — | Explicit start ref (SHA, branch, tag, or ISO date) |
| `--to` | `HEAD` | Explicit end ref |
| `--connector`, `-c` | all | Connector name(s) to send output to |
| `--dry-run` | false | Skip LLM and connectors; print plain summary to stdout |

---

## Prompt Templates

Templates in `Prompt:Template` use `{{double-brace}}` tokens:

| Token | Value |
|---|---|
| `{{repo.name}}` | Repository name from config |
| `{{time_window}}` | `fromSpec to toSpec` string |
| `{{diff_summary}}` | Formatted commit list and stats |

---

## Development

### Running Tests

```bash
# TUnit runs via dotnet run, not dotnet test
dotnet run --project GitGab.Tests/GitGab.Tests.csproj
# Expected: 55 tests, 0 failures
```

### Project Structure

```
GitGab/
  Commands/          # System.CommandLine command handlers
  Models/            # Config, Git, LLM, Connector POCOs
  Services/
    Config/          # ConfigurationService — typed config accessors
    Git/             # GitService (LibGit2Sharp), DiffService
    LLM/             # ILLMProvider + four provider implementations
    Summary/         # PromptBuilder, SummaryService
    Connector/       # IConnector + five connector implementations
GitGab.Tests/        # TUnit test suite
```

See `AGENTS.md` for in-depth guidance on conventions and design decisions.

### Adding an LLM Provider

1. Implement `ILLMProvider` in `Services/LLM/Providers/`
2. Add a case in `LLMProviderFactory.CreateProvider`
3. Register any new dependencies in `Program.cs`

### Adding a Connector

1. Implement `IConnector` in `Services/Connector/Providers/`
2. Add a config subtype in `Models/Config/ConnectorConfig.cs`
3. Add a case in `ConnectorFactory`
4. Handle the new type in `ConfigurationService.GetConnectors`

---

## Environment Variables

```bash
GEMINI_API_KEY=...
OPENAI_API_KEY=...
ANTHROPIC_API_KEY=...

GITHUB_TOKEN=...
GITLAB_TOKEN=...

SLACK_WEBHOOK_URL=...

SMTP_HOST=...
SMTP_USER=...
SMTP_PASS=...

# Set to "Development" to load appsettings.Development.json
DOTNET_ENVIRONMENT=Development
```

---

## What's Not Yet Implemented

- `gitgab server` — HTTP API mode
- `gitgab repo add/remove` — configuration persistence
- `gitgab config validate` — full validation logic
- Cron scheduling (NCrontab is wired in, scheduler is not)
- Polly retry policies on LLM and connector calls
- Token chunking for diffs that exceed LLM context limits
- Diff filtering by file pattern, author, or directory
- `GetDiffSinceLastTag` (currently requires a tag named `"last-tag"`)

---

## License

MIT
