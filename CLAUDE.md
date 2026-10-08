# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

USCIS Form Change Tracker: scrapes the USCIS all-forms page, extracts text from every form PDF, hashes it, and when a hash changes stores a line-by-line diff and emails the mailing list.

## Architecture

Two deployable services under `src/` (tests under `tests/`), backed by PostgreSQL (EF Core / Npgsql):

- `USCISFormTracker.Processor`: worker that runs the monitoring job on a Quartz cron schedule (daily by default) and emails the run summary
- `USCISFormTracker.Web`: mailing-list signup (`POST /mailing-list`) and recent changes feed (`GET /changes/recent`)

Shared libraries: `Core` (business logic), `Data` (EF Core + migrations), `Formatting` (diff/summary formatting for email and web), `Email` (Mailgun client; `IEmailSender` sends mail and adds list members, `IRunSummaryNotifier` emails a run summary). There is no message bus: Processor and Web call the Email library directly.

Core is interface-based. Key abstractions and their implementations:

| Interface | Implementation | Role |
|---|---|---|
| `IWebPdfGetter` | `UscisWebPdfGetter` | Scrapes the all-forms page for detail links (XPath `//a[@class='link link--form-title']`), then each detail page for its `.pdf` link |
| `IPdfReader` | `PdfPigLayoutPdfReader` | Extracts text with PdfPig following the PDF's letter rendering order, drops footers (bottom 80pt) by Y position, and groups words within 3pt into lines |
| `IHasher` | `Sha256Hasher` | Hashes extracted text |
| `IDiffer` | `DiffPlexDiffer` | Line-by-line diff (DiffPlex / Myers) producing `DiffLines` |
| `IFormRepository` | in `Data` | Persists `PdfFormRecord` (current hash per form) and `PdfFormChange` (diff + old/new hash per detected change) |
| `IPdfFileManager` | `PdfFileManager` | Stores downloaded PDFs under `{PdfStorage:RootDirectory}/uscis/{form}/{form}_{timestamp}.pdf` and prunes old versions. The DB stores paths relative to the root; `uscis` is a constant in `FormComparisonService` |

`PdfPigReader` (raw `page.Text`) and `ImprovedPdfPigReader` (Y-position grouping with header filtering) are alternative readers kept for comparison in tests. `PdfPigLayoutPdfReader` is the one registered in `Core/ServiceExtensions.cs`; changing the reader changes every stored hash, so expect a full round of "changes" after swapping it.

**Data flow:** scrape PDF links -> download -> extract text -> hash -> compare with stored `PdfFormRecord` -> on change, diff, store `PdfFormChange`, update record -> after the run, `IRunSummaryNotifier` sends one summary email to the Mailgun mailing list if anything changed.

## Development

```bash
docker compose up -d postgres            # infrastructure only
dotnet build
dotnet test
dotnet run --project src/USCISFormTracker.Processor
HTTP_PORT=5080 dotnet run --project src/USCISFormTracker.Web
```

Full stack via Docker: `cp .env.example .env`, then `docker compose up -d`. `docker-compose.yml` is the production file: the web container listens on http://127.0.0.1:8080 on the host and is reached publicly through the Cloudflare Tunnel (`cloudflared`), which needs `CLOUDFLARE_TUNNEL_TOKEN` in `.env`.

For local Docker runs copy `docker-compose.override.example.yml` to `docker-compose.override.yml` (gitignored); `docker compose up` merges it automatically. The override moves `cloudflared` into the `tunnel` profile so it is skipped unless you pass `--profile tunnel`. Production hosts must not have an override file.

## Comments

Comments describe only the file they are in. Do not reference other files, overrides, or workflows from a comment (for example, `docker-compose.yml` must not mention the override file). Cross-file workflow guidance lives in this file.

## Testing

xUnit + Moq in `tests/USCISFormTracker.Tests`. Fixtures live in `TestData/Html` (scraped USCIS pages) and `TestData/Pdf` (form PDFs plus the `PdfTest_First/Second` pair used for diff tests). `TestHelpers/MockHttpMessageHandler` stubs HTTP for scraping and end-to-end tests. Several tests (`PdfInspectorTests`, `PdfTextAnalysisTests`, `DiffInspectionTests`) exist to inspect extraction quality rather than assert behavior.

## Configuration

Committed `appsettings.json` files hold placeholders and non-sensitive defaults only. Real values (Mailgun key, database password, Cloudflare tunnel token) go in `.env` (template in `.env.example`, loaded via DotNetEnv) or environment variables. Database settings are read as flat `DATABASE_*` keys in `Data/ServiceExtensions.cs` and Mailgun settings as `MAILGUN_*` keys in `Email/ServiceExtensions.cs`; both services fail at startup if a required key is missing. `.env`, `appsettings.*.json` variants, `docker-compose.override.yml`, and the PDF storage root `forms/` are gitignored.
