# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

USCIS Form Change Tracker: scrapes the USCIS all-forms page, extracts text from every form PDF, hashes it, and when a hash changes stores a line-by-line diff and emails the mailing list.

## Architecture

Three deployable services under `src/` (tests under `tests/`), communicating over RabbitMQ (MassTransit) and backed by PostgreSQL (EF Core / Npgsql):

- `USCISFormTracker.Processor`: worker that runs the monitoring job on a Quartz cron schedule (daily by default)
- `USCISFormTracker.Emailer`: consumes change events and sends Mailgun notifications
- `USCISFormTracker.Web`: mailing-list signup (`POST /mailing-list`) and recent changes feed (`GET /changes/recent`)

Shared libraries: `Core` (business logic), `Data` (EF Core + migrations), `Dto` (message contracts), `Formatting` (diff/summary formatting for email and web).

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

**Data flow:** scrape PDF links -> download -> extract text -> hash -> compare with stored `PdfFormRecord` -> on change, diff, store `PdfFormChange`, publish event (Emailer sends the notification) -> update record.

## Development

```bash
docker compose up -d postgres rabbitmq   # infrastructure only
dotnet build
dotnet test
dotnet run --project src/USCISFormTracker.Processor
dotnet run --project src/USCISFormTracker.Emailer
HTTP_PORT=5080 dotnet run --project src/USCISFormTracker.Web
```

Full stack via Docker: `cp .env.example .env`, then `docker compose up -d`. For local Docker runs also copy `docker-compose.override.example.yml` to `docker-compose.override.yml`; it publishes the web UI on localhost and disables the Cloudflare Tunnel. See README.md for details.

## Testing

xUnit + Moq in `tests/USCISFormTracker.Tests`. Fixtures live in `TestData/Html` (scraped USCIS pages) and `TestData/Pdf` (form PDFs plus the `PdfTest_First/Second` pair used for diff tests). `TestHelpers/MockHttpMessageHandler` stubs HTTP for scraping and end-to-end tests. Several tests (`PdfInspectorTests`, `PdfTextAnalysisTests`, `DiffInspectionTests`) exist to inspect extraction quality rather than assert behavior.

## Configuration

Committed `appsettings.json` files hold placeholders and non-sensitive defaults only. Real values (Mailgun key, database/RabbitMQ passwords, Cloudflare tunnel token) go in `.env` (template in `.env.example`, loaded via DotNetEnv) or environment variables. `.env`, `appsettings.*.json` variants, `docker-compose.override.yml`, and the PDF storage root `forms/` are gitignored.
