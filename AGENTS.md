# AGENTS.md — GitGab Agent Guidance

This file gives AI agents context about the GitGab codebase: what it does, how it's structured, how to build and test it, and the conventions to follow when making changes.

---

## What is GitGab?

GitGab is a .NET 10 CLI tool that:
1. Clones/pulls one or more git repositories (via LibGit2Sharp)
2. Computes diffs over a configurable time window (ISO 8601 durations like `P7D`, `PT6H`)
3. Sends the diff to an LLM provider (Gemini, OpenAI, Anthropic, or a local model) to generate a change summary
4. Distributes that summary via one or more connectors (Slack, Email, file, webhook, console)

It is driven entirely from `appsettings.json` + environment variables — no database, no web framework.

---

## Solution Layout

```
GitGab.slnx                      # .NET solution (XML format)
spec.md                          # Full feature spec and open design questions — read before making big changes
GitGab/
  Program.cs                     # Entry point; builds DI host and registers System.CommandLine commands
  appsettings.json               # Default config — repos, LLM, connectors, schedule
  appsettings.Development.json   # Dev overrides (API keys, local repo paths)
  Commands/
    AnalyzeCommand.cs            # `gitgab analyze` — main workflow orchestrator
    RepoCommand.cs               # `gitgab repo list|add|remove`
    ConnectorCommand.cs          # `gitgab connector test`
    ConfigCommand.cs             # `gitgab config validate`
    ServerCommand.cs             # `gitgab server` (stub — not yet implemented)
  Models/
    Config/                      # AppSettings, RepositoryConfig, LLMConfig, ConnectorConfig (+ subtypes)
    Git/                         # RepositoryInfo, CommitInfo, FileChange, DiffResult, GitStats
    LLM/                         # PromptRequest, PromptResponse, UsageInfo
    Connector/                   # ConnectorMessage, ConnectorResult
  Services/
    Config/ConfigurationService.cs   # Typed config accessors; resolves ${ENV_VAR} tokens in values
    Git/
      GitService.cs              # Clone, pull, diff — all LibGit2Sharp logic lives here
      DiffService.cs             # Higher-level diff helpers: time-window, ref-to-ref, since-last-tag
    LLM/
      ILLMProvider.cs            # Single-method interface: GenerateAsync(PromptRequest)
      LLMProviderFactory.cs      # Creates provider by name; reads default from LLM:Provider config
      Providers/
        GeminiProvider.cs        # Google Gemini via REST
        OpenAiProvider.cs        # OpenAI / compatible API
        AnthropicProvider.cs     # Anthropic Claude via REST
        LocalProvider.cs         # Local LLM (Ollama-style OpenAI-compatible endpoint)
    Summary/
      PromptBuilder.cs           # Replaces {{repo.name}}, {{time_window}}, {{diff_summary}} in templates
      SummaryService.cs          # GenerateSummaryAsync (uses LLM) + GenerateSimpleSummary (dry-run)
    Connector/
      IConnector.cs              # Single-method interface: SendAsync(ConnectorMessage)
      ConnectorFactory.cs        # Creates connectors by name or returns all configured ones
      Providers/
        SlackConnector.cs        # HTTP POST to Slack Incoming Webhook
        EmailConnector.cs        # SMTP via MailKit
        FileConnector.cs         # Write markdown/JSON to disk; supports {{repo.name}} in path
        WebhookConnector.cs      # Generic HTTP POST
        ConsoleConnector.cs      # Writes to stdout — used for dry-run and testing
GitGab.Tests/
  Services/
    Config/ConfigurationServiceTests.cs
    Git/DiffServiceTests.cs
    Summary/PromptBuilderTests.cs
    Summary/SummaryServiceTests.cs
    Connector/ConnectorTests.cs   # ConnectorFactory + ConsoleConnector + FileConnector tests
```

---

## Build & Test

```bash
# Build (warnings are errors — see below)
dotnet build

# Run all tests
dotnet run --project GitGab.Tests/GitGab.Tests.csproj

# Run the CLI
dotnet run --project GitGab -- analyze --dry-run --all
dotnet run --project GitGab -- analyze --repo my-app --time-window P7D --dry-run
```

Tests use **TUnit** (not xUnit/NUnit). TUnit's runner is the test project executable itself — run it with `dotnet run`, not `dotnet test`. The test project targets `net10.0` and has `OutputType=Exe`.

Expected result: **55 tests, 0 failures**.

---

## Code Conventions

### Compiler strictness
Both projects have `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Any new C# warning will break the build. NuGet audit warnings (`NU####`) are exempted via `<WarningsNotAsErrors>`.

### Nullable reference types
Enabled globally. Annotate everything. Avoid `!` null-forgiveness unless you have a concrete reason.

### Dependency injection
All services are registered as **singletons** in `Program.cs`. If a new service needs to be added, register it there. Services receive their dependencies via constructor injection.

### LibGit2Sharp import alias
`GitService.cs` uses:
```csharp
using LibGitCommands = LibGit2Sharp.Commands;
```
This is intentional — `LibGit2Sharp.Commands` collides with the `GitGab.Commands` namespace. Always use the alias when calling `LibGitCommands.Fetch(...)` or `LibGitCommands.Pull(...)`.

### SSH auth
`SshAgentCredentials` was removed in LibGit2Sharp 0.30. Use `DefaultCredentials` for SSH — libgit2 delegates to the system ssh-agent automatically.

### Time windows
Time windows are **ISO 8601 durations** (`P7D`, `PT6H`, `P1DT12H`). The `DiffService.ParseTimeWindow` method converts them to `TimeSpan` via `XmlConvert.ToTimeSpan`. Invalid input falls back to 7 days with a warning log.

### Prompt templates
Templates use `{{double-brace}}` syntax and are processed by `PromptBuilder.BuildPrompt`. Available tokens: `{{repo.name}}`, `{{time_window}}`, `{{diff_summary}}`. Templates are stored in `appsettings.json` under `Prompt:Template`.

### Configuration
- Typed accessors live in `ConfigurationService` — don't call `IConfiguration` directly from services; go through `ConfigurationService`.
- `${ENV_VAR}` tokens in appsettings values are resolved at runtime by the config system.

---

## Key Design Decisions

- **LibGit2Sharp over `git` process**: avoids shelling out, gives type-safe access to git objects, and handles credentials programmatically.
- **ILLMProvider interface**: all four providers (Gemini, OpenAI, Anthropic, Local) share the same `GenerateAsync(PromptRequest)` contract. To add a new provider, implement `ILLMProvider`, add a case to `LLMProviderFactory`, and register with DI.
- **IConnector interface**: connectors follow the same pattern. To add a new connector, implement `IConnector`, add a type string to `ConnectorFactory`, and add a config subtype in `Models/Config/ConnectorConfig.cs`.
- **No async in GitService**: LibGit2Sharp is synchronous; git operations run on the calling thread. `AnalyzeCommand` wraps multi-repo work in `Task.WhenAll` for concurrency.
- **Scriban 7.x**: used for connector output formatting (not the prompt system, which uses simple string replace in PromptBuilder). Upgraded from 5.x due to critical CVEs.
- **MailKit 4.17**: used for email. BouncyCastle comes in as a transitive dep — there is no direct BouncyCastle usage in application code.

---

## What's Not Yet Implemented

These are stubs or incomplete — check `spec.md` for full intended behaviour:

- `ServerCommand` — HTTP API server mode (`gitgab server`)
- `RepoCommand` add/remove sub-commands (print placeholder messages)
- `ConfigCommand` validate
- Scheduled/cron-based automatic runs (NCrontab is a dependency but the scheduler isn't wired up)
- Diff filtering by file pattern, author, or directory
- Token chunking for large diffs
- Retry policies (Polly is a dependency but not yet applied to LLM/connector calls)
- `GetDiffSinceLastTag` in `DiffService` — currently passes `"last-tag"` as a literal ref, which will fail unless a tag named `last-tag` exists

---

## Environment Variables

```bash
# LLM
GEMINI_API_KEY=...
OPENAI_API_KEY=...
ANTHROPIC_API_KEY=...

# Git auth (used in appsettings via ${VAR} token substitution)
GITHUB_TOKEN=...
GITLAB_TOKEN=...

# Email connector
SMTP_HOST=...
SMTP_USER=...
SMTP_PASS=...

# Slack
SLACK_WEBHOOK_URL=...

# Runtime
DOTNET_ENVIRONMENT=Development   # loads appsettings.Development.json
```

---

## Package Notes

| Package | Version | Why pinned |
|---|---|---|
| LibGit2Sharp | 0.32.0 | Latest stable; SSH cred types changed in 0.30 |
| MailKit | 4.17.0 | Minimum version with patched BouncyCastle transitive dep |
| Scriban | 7.2.6 | 5.x had critical CVEs; 7.x is the fixed major version |
| TUnit | 1.63.0 | Microsoft.Testing.Platform runner — not compatible with `dotnet test` on .NET 10 without extra flags |
| Polly | 7.2.3 | v8 has a breaking API; upgrade is a separate task |
