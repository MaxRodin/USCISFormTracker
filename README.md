# USCIS Form Tracker

Monitors [USCIS immigration forms](https://www.uscis.gov/forms/all-forms) for changes and emails subscribers a line-by-line diff whenever a form's PDF is updated.

USCIS revises its forms without much fanfare, and filing an outdated edition can get an application rejected. This tracker checks every published form daily and reports exactly what changed.

## How It Works

1. A scheduled job (Quartz, daily by default) scrapes the USCIS all-forms page to discover every form's PDF.
2. Each PDF's text is extracted with [PdfPig](https://github.com/UglyToad/PdfPig), using position-based filtering to strip headers/footers and preserve line structure.
3. A SHA-256 hash of the extracted text is compared against the last known hash stored in PostgreSQL.
4. When a hash differs, a line-by-line diff is generated with [DiffPlex](https://github.com/mmanela/diffplex) and the change is recorded.
5. A message is published to RabbitMQ, and the emailer service sends a notification with the diff to the mailing list via [Mailgun](https://www.mailgun.com/).

## Architecture

Three services communicating over RabbitMQ (MassTransit), backed by PostgreSQL:

| Project | Role |
|---|---|
| `USCISFormTracker.Processor` | Worker service that runs the scheduled monitoring job: scrape, download, hash, diff, persist |
| `USCISFormTracker.Emailer` | Consumes change events from RabbitMQ and sends Mailgun notifications |
| `USCISFormTracker.Web` | Public site: mailing-list signup (`POST /mailing-list`) and recent changes feed (`GET /changes/recent`) |
| `USCISFormTracker.Core` | Business logic: scraping, PDF text extraction, hashing, diffing |
| `USCISFormTracker.Data` | EF Core persistence (PostgreSQL) and migrations |
| `USCISFormTracker.Dto` | Message contracts shared between services |
| `USCISFormTracker.Formatting` | Formats diffs and run summaries for email and web output |
| `USCISFormTracker.Tests` | xUnit test suite with HTML/PDF fixtures |

## Running with Docker

```bash
cp .env.example .env
# Edit .env — Mailgun credentials are required; database/RabbitMQ
# passwords have development defaults you should change for production.

docker-compose up -d
```

This starts PostgreSQL, RabbitMQ, and all three services. See [DOCKER.md](DOCKER.md) for the full deployment guide.

## Local Development

Requires the .NET 8 SDK, plus PostgreSQL and RabbitMQ (easiest via `docker-compose up -d postgres rabbitmq`).

```bash
dotnet build
dotnet test

# Run individual services
dotnet run --project USCISFormTracker.Processor
dotnet run --project USCISFormTracker.Emailer
HTTP_PORT=5080 dotnet run --project USCISFormTracker.Web  # default port 80 needs root on Linux
```

The web service serves HTTP-only unless a PFX certificate exists at the path given
by `HTTPS_CERT_PATH` (default `/app/certs/origin.pfx`), in which case HTTPS on
port 443 is enabled automatically.

Configuration comes from each service's `appsettings.json` (committed, placeholders only) overridden by environment variables / a local `.env` file. Never commit real credentials — `.env` and `appsettings.*.json` variants are gitignored.

## Notes

- PDF text extraction quality matters for diff quality: the `ImprovedPdfPigReader` filters page headers/footers by Y-position and groups words into lines so diffs align with the document's real line structure.
- Known issues and planned improvements are tracked in [ISSUES.md](ISSUES.md).
