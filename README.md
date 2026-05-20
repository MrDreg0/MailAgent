# MailAgent

MailAgent is a .NET-based mail ingestion and release digesting prototype.

It connects to a mail server (EWS or IMAP), converts message bodies to Markdown, stores unique mails in PostgreSQL, and builds release-oriented daily digests with a local or self-hosted LLM. The solution also includes a Blazor web UI for browsing stored mails and reading persisted daily digests.

## Current Scope

MailAgent currently focuses on one practical workflow:

1. Import release-related emails from a mailbox into PostgreSQL.
2. Normalize and store mail bodies as Markdown.
3. Generate persisted daily digests from already stored mails.
4. Browse both raw mails and generated digests in the web UI.

The project is intentionally application-centric for now. There is no full domain layer yet; the current architecture is centered around `Application + Infrastructure + thin hosts`.

## Features

- Mail import from `EWS` or `IMAP`
- Incremental background import with overlap-based deduplication
- HTML/text mail body conversion to Markdown
- PostgreSQL persistence with `MessageId`-based uniqueness
- Release-oriented daily digest generation from stored mails only
- Two-stage LLM workflow for digests:
  - fast-model mail classification and normalization
  - main-model final digest generation
- Configurable digest output language (`Russian` or `English`)
- Blazor Web UI for:
  - mail archive browsing
  - daily digest archive browsing
  - digest regeneration for a specific day

## Solution Structure

- `MailAgent.Api`
  ASP.NET Core host and composition root for the API and hosted background services.
- `MailAgent.Web`
  Blazor Web App for browsing stored mails and daily digests.
- `MailAgent.Application`
  Application use cases, contracts, digest logic, and provider-neutral LLM abstraction.
- `MailAgent.Mail`
  Mail provider adapters for EWS and IMAP.
- `MailAgent.Database`
  Persistence models and repository implementations.
- `MailAgent.Database.PostgreSql`
  PostgreSQL EF Core wiring and migrations.
- `MailAgent.Api.Contracts`
  Shared HTTP contracts used by the web app when calling the API.
- `*.Tests`
  Unit and wiring tests for the main layers.

## Daily Digest Flow

Daily digests are generated from mails that are already stored in PostgreSQL.

The current pipeline is:

1. Load mails for one UTC day and folder.
2. Ask a fast model to classify which mails are relevant for the digest.
3. Ask the fast model again to normalize each selected mail into a short meaningful summary.
4. Ask the main model to build the final markdown digest from those normalized summaries.
5. Persist the digest in the database.

This keeps the web app read-oriented and avoids direct LLM wiring inside `MailAgent.Web`.

## Tech Stack

- .NET 10
- ASP.NET Core Minimal API
- Blazor Web App
- Entity Framework Core
- PostgreSQL
- Refit
- FluentValidation
- BlazorBootstrap
- Local/self-hosted LLM providers:
  - LM Studio (OpenAI-compatible endpoint)
  - Ollama

## Running Locally

### 1. Prerequisites

- .NET SDK 10
- Docker and Docker Compose
- For running the full application: a reachable mail server
- For digest generation at runtime: a running LLM provider:
  - LM Studio, or
  - Ollama

The automated test suite does not require a real mail server, PostgreSQL instance, or LLM provider.

### 2. Configure environment

`compose.yaml` expects environment variables for PostgreSQL, mail access, LLM access, and digest language.

Copy `.env.example` to `.env` and replace placeholders with local values. A typical local `.env` should define:

```env
POSTGRES_USER=postgres
POSTGRES_PASSWORD=change-me
POSTGRES_DB=mailagent

MAIL_PROVIDER=Ews
MAIL_USERNAME=your-login
MAIL_PASSWORD=your-password
MAIL_EWS_URL=https://mail.example.com/EWS/Exchange.asmx
MAIL_EWS_DOMAIN=EXAMPLE

LLM_PROVIDER=lmstudio
LLM_BASE_URL=http://host.docker.internal:1234
LLM_FAST_MODEL=your-fast-model
LLM_MAIN_MODEL=your-main-model

PREFER_LANGUAGE=English
```

Notes:

- The current compose setup is wired for EWS by default.
- `LLM_BASE_URL=http://host.docker.internal:1234` is a common LM Studio setup when the API runs in Docker and LM Studio runs on the host.
- Ollama can also be used by setting `LLM_PROVIDER=ollama` and pointing `LLM_BASE_URL` to the reachable Ollama endpoint.
- `PREFER_LANGUAGE` is mapped to `DailyDigest__OutputLanguage`.
- Valid output language values are currently:
  - `Russian`
  - `English`

### 3. Start services

```bash
docker compose up --build
```

Local endpoints:

- API: [http://localhost:8080](http://localhost:8080)
- Web UI: [http://localhost:8081](http://localhost:8081)

## Configuration Highlights

Important API configuration sections:

- `ConnectionStrings:Database`
- `MailServer`
  - `Provider`
  - `Username`
  - `Password`
  - `Ews` / `Imap`
- `Llm`
  - `Provider`
  - `BaseUrl`
  - `TimeoutMinutes`
  - `FastModel`
  - `MainModel`
- `MailImport`
  - `Enabled`
  - `RunOnStartup`
  - `Interval`
  - `InitialLookbackPeriod`
  - `OverlapPeriod`
  - `Folders`
- `DailyDigest`
  - `Enabled`
  - `RunOnStartup`
  - `Interval`
  - `GenerateAfter`
  - `InitialBackfillPeriod`
  - `Folder`
  - `OutputLanguage`

The application validates required configuration on startup and is designed to fail fast on missing or invalid host settings.

## Web UI

The web app currently contains two primary screens:

- `/messages`
  Stored mail archive
- `/daily-digests`
  Persisted daily digest archive with per-day regeneration

The UI is built with `BlazorBootstrap` and follows a grid + detail-view pattern.

## Verification

Main verification command:

```bash
dotnet test MailAgent.sln
```

This command is intended to work from a clean clone after restoring NuGet packages. It exercises unit and wiring tests only; external EWS/IMAP servers, PostgreSQL containers, and LLM providers are runtime prerequisites, not test prerequisites.

## Current Status

This repository is still a working prototype, but it already covers a useful end-to-end workflow:

- import mails,
- store them safely,
- generate persisted release digests,
- inspect the result in a web UI.

Areas that are still intentionally lightweight:

- no full domain model yet
- limited end-to-end integration coverage against real providers
- local-LLM prompt tuning is still iterative

## License

No license has been defined yet.
