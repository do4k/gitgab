# GitGab - AI-Powered Repository Change Summarizer

## Overview

GitGab is a tool that monitors one or more git repositories, computes diffs over configurable time periods, and generates AI-powered summaries of changes that can be distributed via multiple connectors (Slack, Email, etc.).

## Core Features

### 1. Repository Management
- Clone single or multiple repositories
- Support for GitHub, GitLab, Bitbucket, and Git Enterprise
- Configurable authentication (SSH keys, HTTPS with tokens, personal access tokens)
- Repository groups/collections for batch processing
- Configurable clone depth and pruning options

### 2. Diff Computation
- Compute diffs between two points in time
- Configurable time window (default: 1 week)
- Support for:
  - Time-based diffs (e.g., "last 7 days")
  - Branch-based diffs (e.g., "main since last tag")
  - Commit range diffs (e.g., "v1.0.0..v1.1.0")
  - Tag-based diffs
- Filter by file patterns (include/exclude)
- Stats: files changed, lines added/removed, commits count, authors

### 3. AI Summary Generation
- Connect to configurable LLM providers:
  - **Gemini 2.5 Flash** (primary)
  - OpenAI (GPT-4, GPT-3.5)
  - Anthropic (Claude)
  - Local/self-hosted LLMs (via OpenAI-compatible API)
- Configurable prompts/templates for summary generation
- Support for custom system prompts per repository
- Token usage tracking and rate limiting awareness
- Temperature and creativity controls

### 4. Connector System
- **Slack**: Post to channel with formatted messages
- **Email**: Send via SMTP with HTML/text templates
- **Microsoft Teams**: Webhook integration
- **Discord**: Webhook integration
- **Mattermost**: Webhook integration
- **File output**: Write to markdown/JSON/HTML files
- **Webhook**: Generic HTTP POST for custom integrations
- **Console**: Simple stdout for testing/pipe to other tools

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         GitGab CLI/App                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐ │
│  │   Config     │    │  Repository   │    │    LLM           │ │
│  │   Manager    │───▶│   Manager    │───▶│   Provider       │ │
│  └──────────────┘    └──────────────┘    └──────────────────┘ │
│          ▲                  │  ▲                    ▲          │
│          │                  │  │                    │          │
│          ▼                  ▼  │                    │          │
│  ┌──────────────┐    ┌────────┐     │    ┌──────────────┐  │
│  │   Scheduler  │    │  Diff   │─────┘    │  Prompt       │  │
│  │   (Cron/     │    │  Engine │         │  Builder      │  │
│  │   Manual)    │    └────────┘         └──────────────┘  │
│  └──────────────┘              │                         │      │
│                               ▼                         ▼      │
│                        ┌──────────────┐    ┌──────────────┐   │
│                        │  Summary     │◀───│   AI         │   │
│                        │  Generator   │    │   Client     │   │
│                        └──────────────┘    └──────────────┘   │
│                               │                                 │
│                               ▼                                 │
│                        ┌──────────────┐                        │
│                        │  Connector   │                        │
│                        │   Manager    │                        │
│                        └──────────────┘                        │
│                               │                                 │
│        ┌──────────────────────┼──────────────────────┐       │
│        ▼                      ▼                      ▼       │
│  ┌─────────────┐      ┌─────────────┐      ┌─────────────┐   │
│  │   Slack     │      │    Email    │      │   Webhook   │   │
│  └─────────────┘      └─────────────┘      └─────────────┘   │
│                                                              │
└─────────────────────────────────────────────────────────────────┘
```

### Component Details

#### Configuration Management
- YAML/JSON config file support (`Microsoft.Extensions.Configuration` + `YamlDotNet`)
- Environment variable overrides (built-in .NET configuration)
- Command-line argument overrides (`System.CommandLine` or `Microsoft.Extensions.Configuration.CommandLine`)
- Multiple config profiles (e.g., `appsettings.Production.json`, `appsettings.Development.json`)
- Secure credential storage (Azure Key Vault, AWS Secrets Manager, or encrypted user secrets)
- Strongly-typed configuration via C# classes with `IOptions<T>` pattern

#### Repository Manager
- Git operations via `LibGit2Sharp` (native .NET Git library)
- Async/await pattern for concurrent repo operations
- Automatic retry with exponential backoff (using `Polly`)
- Shallow clone support for large repos
- Repository health checks and error handling
- Cancellation support via `CancellationToken`

#### Diff Engine
- Efficient diff computation using `LibGit2Sharp` tree/diff APIs
- Caching layer for performance (store computed diffs) - `Microsoft.Extensions.Caching.Memory`
- Parallel diff computation across repositories (`Task.WhenAll`)
- Filter by:
  - File extensions
  - Directory paths
  - Author emails
  - Commit messages (regex via `System.Text.RegularExpressions`)

#### LLM Provider Abstraction
- Unified interface for all LLM providers
- Provider-specific configuration:
  ```yaml
  providers:
    gemini:
      api_key: "..."
      model: "gemini-2.5-flash"
      base_url: "https://generativelanguage.googleapis.com"
      timeout: 60
      max_tokens: 8192
    openai:
      api_key: "..."
      model: "gpt-4"
      base_url: "https://api.openai.com"
    anthropic:
      api_key: "..."
      model: "claude-3-sonnet"
      base_url: "https://api.anthropic.com"
    local:
      base_url: "http://localhost:11434"
      model: "llama3.2"
  ```

#### Prompt Builder
- Template system for summary prompts (string interpolation or `Scriban` for advanced templating)
- Dynamic prompt construction based on diff context
- Support for:
  - Markdown output
  - JSON output (structured data via `System.Text.Json`)
  - Custom formats
- Prompt versioning and A/B testing

#### Summary Generator
- Processes diff data into LLM-optimized format
- Chunking for large diffs (respecting token limits)
- Handles:
  - Code changes with syntax highlighting hints
  - Commit messages
  - Author information
  - File statistics
  - Linked issues/PRs (if detectable from commit messages)
- Uses `System.Text.Json` for serialization/deserialization

#### Connector Manager
- Pluggable connector architecture (DI via `Microsoft.Extensions.DependencyInjection`)
- Retry logic with exponential backoff (`Polly` library)
- Rate limiting per connector type (`Polly` rate limiting)
- Template system for output formatting (string templates or `Scriban`)
- Support for multiple connectors per summary
- Async I/O throughout (`HttpClient`, streams)

## Configuration Schema

### Main Configuration (`config.yaml`)

```yaml
# Global settings
name: "GitGab"
log_level: "info"
log_file: "/var/log/gitgab/app.log"

# Default time window for diffs (ISO 8601 duration)
default_time_window: "P7D"  # 7 days

# Where to store cloned repos
repo_cache_dir: "/var/cache/gitgab/repos"

# Maximum concurrent operations
max_concurrent_repos: 5
max_concurrent_llm_calls: 3

# Repository definitions
repositories:
  - name: "my-app"
    url: "git@github.com:org/my-app.git"
    auth:
      type: "ssh"
      # or type: "https"
      # token: "${GITHUB_TOKEN}"  # from env var
    branch: "main"
    time_window: "P7D"  # override default
    
  - name: "enterprise-repo"
    url: "https://git.enterprise.com/org/repo.git"
    auth:
      type: "https"
      username: "user"
      password: "${ENT_TOKEN}"
    diff:
      from: "v1.0.0"
      to: "v1.1.0"

# LLM Provider (default)
llm:
  provider: "gemini"
  model: "gemini-2.5-flash"
  api_key: "${GEMINI_API_KEY}"
  temperature: 0.3
  max_tokens: 4096

# Summary prompt template
prompt:
  template: |-
    You are an expert software engineer. Analyze the following git changes
    and provide a concise, informative summary.
    
    Repository: {{repo.name}}
    Period: {{time_window}}
    
    Changes:
    {{diff_summary}}
    
    Please provide:
    1. A high-level summary of what changed (2-3 sentences)
    2. Key features or bug fixes
    3. Potential impact/risks
    4. Notable code patterns or architectural changes
    
    Format your response as Markdown.

# Connectors
connectors:
  - type: "slack"
    name: "team-channel"
    webhook_url: "${SLACK_WEBHOOK}"
    channel: "#engineering-updates"
    template: "slack-default"
    
  - type: "email"
    name: "team-email"
    smtp:
      host: "smtp.gmail.com"
      port: 587
      username: "${SMTP_USER}"
      password: "${SMTP_PASS}"
    from: "gitgab@company.com"
    to:
      - "engineering@company.com"
    subject: "Weekly Code Changes: {{repo.name}}"
    
  - type: "file"
    name: "local-output"
    path: "/var/output/gitgab/{{repo.name}}-{{date}}.md"

# Scheduling
schedule:
  # Run every Monday at 9am
  cron: "0 9 * * 1"
  # or manual only
  # enabled: false
```

## CLI Interface

```bash
# Run a single analysis
gitgab analyze --repo my-app --time-window P7D --output slack

# Run for all configured repos
gitgab analyze --all

# Run with custom diff range
gitgab analyze --repo my-app --from v1.0.0 --to v1.1.0

# Run and send to specific connector
gitgab analyze --repo my-app --connector slack --connector email

# Dry run (no connectors)
gitgab analyze --repo my-app --dry-run

# List configured repos
gitgab repo list

# Add a new repo
gitgab repo add --name new-app --url https://github.com/org/new-app.git

# Test connector
gitgab connector test --name team-slack

# Validate config
gitgab config validate

# Run in server mode (HTTP API)
gitgab server --port 8080
```

## HTTP API (Optional Server Mode)

```
POST /api/v1/analyze
{
  "repositories": ["my-app", "other-app"],
  "time_window": "P7D",
  "connectors": ["slack", "email"],
  "llm_provider": "gemini"
}

GET /api/v1/repos
GET /api/v1/repos/{name}
GET /api/v1/summaries
GET /api/v1/summaries/{id}
POST /api/v1/connectors/test
```

## Data Flow

1. **Trigger**: Scheduler or manual CLI/API call
2. **Fetch**: Repository Manager clones/updates repos
3. **Diff**: Diff Engine computes changes for time window
4. **Format**: Diff data formatted for LLM consumption
5. **Generate**: LLM produces summary
6. **Distribute**: Connector Manager sends to all configured destinations
7. **Log**: All operations logged with results and metrics

## Error Handling

- Repository clone failures: Retry 3x, then skip with error notification
- LLM API failures: Retry with exponential backoff, fallback to alternative provider if configured
- Connector failures: Retry 3x, log error, continue with other connectors
- Token limit errors: Split diff into smaller chunks, retry
- Rate limiting: Respect headers, implement backoff

## Security Considerations

- API keys and tokens stored encrypted at rest
- Support for external secret managers (Vault, AWS Secrets, etc.)
- Minimal permission requirements documented
- No storage of actual code in logs
- Optional anonymization of author emails in outputs
- TLS for all external connections

## Dependencies

### Required
- .NET 11 SDK (latest stable preview or release)
- Git 2.30+
- NuGet packages:
  - `LibGit2Sharp` (Git operations)
  - `Microsoft.Extensions.Http` (HTTP client)
  - `Microsoft.Extensions.Configuration` (configuration with YAML/JSON support)
  - `Microsoft.Extensions.DependencyInjection` (DI container)
  - `Microsoft.Extensions.Logging` (structured logging)
  - `YamlDotNet` (YAML configuration support)
  - `NCrontab` (cron scheduling)
  - `Polly` (retry/backoff policies)

### LLM Provider SDKs (optional, for native API support)
- `Google.Cloud.AI.GenerativeLanguage` (Gemini - via Google Cloud SDK)
- `OpenAI` (OpenAI - community package)
- HTTP client for Anthropic and other providers

### Connector Dependencies
- `Slack.Webhooks` (Slack)
- `MailKit` (SMTP/Email)
- `Microsoft.Extensions.Http` (webhooks, generic HTTP)
- `System.Net.Http.Json` (JSON HTTP utilities)

## Deployment Options

1. **CLI Tool**: Self-contained executable, run via cron or Task Scheduler
2. **Docker Container**: Cross-platform deployment with .NET Docker images
3. **Windows Service**: Run as a background service on Windows
4. **Linux Daemon**: Run as systemd service on Linux
5. **Kubernetes**: CronJob or Deployment with API
6. **GitHub Action**: For repo-specific summaries
7. **Azure App Service / AWS**: PaaS deployment options

## Environment Variables

```bash
# LLM Providers
GEMINI_API_KEY=...
OPENAI_API_KEY=...
ANTHROPIC_API_KEY=...

# Git
GITHUB_TOKEN=...
GITLAB_TOKEN=...
BITBUCKET_TOKEN=...
ENTERPRISE_GIT_TOKEN=...

# Connectors
SLACK_WEBHOOK_URL=...
SMTP_HOST=...
SMTP_USER=...
SMTP_PASS=...

# General
LOG_LEVEL=info
CONFIG_PATH=/etc/gitgab/config.yaml
```

## Example Output

### Slack Message

```markdown
:robot_face: *GitGab Weekly Summary* :robot_face:

*Repository:* `my-app`
*Period:* Last 7 days
*Commits:* 12 | *Files:* 24 | *Lines:* +452/-189

---

:tada: *New Features*
• Added OAuth2 authentication middleware (PR #123)
• New user profile page with avatar upload
• API rate limiting implementation

:bug: *Bug Fixes*
• Fixed memory leak in data loader
• Corrected timezone handling in date picker
• Patched XSS vulnerability in comment display

:art: *Refactoring*
• Migrated from REST to GraphQL for user endpoints
• Extracted authentication logic to shared module
• Updated TypeScript to strict mode

:warning: *Breaking Changes*
• `GET /api/users` now returns paginated results (backwards compatible)
• Removed deprecated `v1/legacy` endpoints

:chart_with_upwards_trend: *Stats*
• TypeScript: +340/-120 lines
• Python: +112/-69 lines
• Tests: +850 lines added

*Generated by GitGab with Gemini 2.5 Flash*
```

### Email Output

HTML email with similar content, plus:
- Repository link
- Direct links to commits/PRs
- Diffstat visualization
- Attachment with full diff (optional)

### JSON Output (for programmatic consumption)

```json
{
  "repository": {
    "name": "my-app",
    "url": "https://github.com/org/my-app",
    "branch": "main"
  },
  "period": {
    "start": "2024-01-01T00:00:00Z",
    "end": "2024-01-08T00:00:00Z",
    "duration": "P7D"
  },
  "stats": {
    "commits": 12,
    "files_changed": 24,
    "lines_added": 452,
    "lines_removed": 189,
    "authors": 4
  },
  "summary": {
    "high_level": "This week focused on authentication improvements and bug fixes...",
    "features": [...],
    "fixes": [...],
    "breaking_changes": [...],
    "refactoring": [...]
  },
  "changes": [
    {
      "commit": "abc123",
      "author": "Jane Doe",
      "message": "Add OAuth2 middleware",
      "files": [...],
      "diff": "..."
    }
  ],
  "metadata": {
    "generated_at": "2024-01-08T09:00:00Z",
    "llm_provider": "gemini",
    "llm_model": "gemini-2.5-flash",
    "tokens_used": 1523
  }
}
```

## Implementation Phases

### Phase 1: Core MVP
- [ ] Repository cloning and caching
- [ ] Basic diff computation (time-based)
- [ ] Single LLM provider (Gemini)
- [ ] Single connector (Slack)
- [ ] CLI interface
- [ ] Configuration file support
- [ ] Basic logging

### Phase 2: Expanded Features
- [ ] Multiple repository support
- [ ] Additional diff strategies (branch, tag, commit range)
- [ ] Multiple LLM providers
- [ ] Multiple connectors (Email, File, Webhook)
- [ ] Diff filtering (by file, author, etc.)
- [ ] Scheduling (cron)

### Phase 3: Production Ready
- [ ] Error handling and retries
- [ ] Rate limiting
- [ ] Token usage tracking
- [ ] Performance optimization (parallel processing)
- [ ] Docker support
- [ ] Documentation
- [ ] Testing framework

### Phase 4: Advanced Features
- [ ] HTTP API server
- [ ] Database for history/stats
- [ ] Web UI for configuration
- [ ] Authentication for API
- [ ] Plugins system for custom connectors/providers
- [ ] GitHub/GitLab webhook integration

## Open Questions

1. Should we support incremental diffs (only summarize new commits since last summary)?
2. Should summaries be stored for historical reference?
3. Should we support comparing across repositories (e.g., "what changed in our microservice cluster")?
4. Should we include code review/comment analysis?
5. Should we integrate with issue trackers (Jira, Linear) to link changes to tickets?
6. Should we support voice/audio summaries?
7. Should we have a "watch mode" that streams changes in real-time?

## Directory Structure (Proposed)

```
gitgab/
├── spec.md                    # This document
├── README.md
├── GitGab.slnx               # Solution file (new XML format for .NET 9+)
├── GitGab/
│   ├── GitGab.csproj          # Main project
│   ├── Program.cs            # Entry point
│   ├── appsettings.json       # Default configuration
│   ├── appsettings.Development.json
│   ├── Models/
│   │   ├── Config/
│   │   │   ├── AppSettings.cs
│   │   │   ├── RepositoryConfig.cs
│   │   │   ├── LLMConfig.cs
│   │   │   └── ConnectorConfig.cs
│   │   ├── Git/
│   │   │   ├── RepositoryInfo.cs
│   │   │   ├── CommitInfo.cs
│   │   │   ├── DiffResult.cs
│   │   │   └── GitStats.cs
│   │   ├── LLM/
│   │   │   ├── PromptRequest.cs
│   │   │   ├── PromptResponse.cs
│   │   │   └── UsageInfo.cs
│   │   └── Connector/
│   │       ├── ConnectorMessage.cs
│   │       └── ConnectorResult.cs
│   ├── Services/
│   │   ├── Config/
│   │   │   ├── ConfigurationService.cs
│   │   │   └── SecretResolver.cs
│   │   ├── Git/
│   │   │   ├── GitService.cs          # Repository management
│   │   │   └── DiffService.cs         # Diff computation
│   │   ├── LLM/
│   │   │   ├── ILLMProvider.cs        # Provider interface
│   │   │   ├── LLMProviderFactory.cs
│   │   │   ├── GeminiProvider.cs      # Gemini implementation
│   │   │   ├── OpenAiProvider.cs     # OpenAI implementation
│   │   │   ├── AnthropicProvider.cs  # Anthropic implementation
│   │   │   └── LocalProvider.cs      # Local LLM support
│   │   ├── Summary/
│   │   │   ├── SummaryService.cs      # Summary generation
│   │   │   └── PromptBuilder.cs       # Prompt building
│   │   └── Connector/
│   │       ├── IConnector.cs          # Connector interface
│   │       ├── ConnectorFactory.cs
│   │       ├── SlackConnector.cs
│   │       ├── EmailConnector.cs
│   │       ├── FileConnector.cs
│   │       ├── WebhookConnector.cs
│   │       └── ConsoleConnector.cs
│   ├── Commands/
│   │   ├── AnalyzeCommand.cs
│   │   ├── RepoCommand.cs
│   │   ├── ConnectorCommand.cs
│   │   └── ConfigCommand.cs
│   └── Server/
│       ├── Controllers/
│       │   ├── AnalyzeController.cs
│       │   ├── ReposController.cs
│       │   └── ConnectorsController.cs
│       ├── Program.cs
│       └── Startup.cs
├── GitGab.Tests/
│   ├── GitGab.Tests.csproj
│   ├── Unit/
│   │   ├── Services/
│   │   └── Models/
│   └── Integration/
├── docs/
│   └── usage.md
└── .github/
    └── workflows/
        ├── build.yml
        └── test.yml
```

---

*Document Version: 0.2.0*  
*Last Updated: 2026-07-30*  
*Status: Draft / Pre-Implementation*  
*Implementation Language: C# / .NET 11*
