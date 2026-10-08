# USCIS Form Tracker

Monitors [USCIS immigration forms](https://www.uscis.gov/forms/all-forms) for changes and emails subscribers a line-by-line diff whenever a form's PDF is updated.

USCIS revises its forms without much fanfare, and filing an outdated edition can get an application rejected. This tracker checks every published form daily and reports exactly what changed.

## How It Works

1. A scheduled job (Quartz, daily by default) scrapes the USCIS all-forms page to discover every form's PDF.
2. Each PDF's text is extracted with [PdfPig](https://github.com/UglyToad/PdfPig), using position-based filtering to strip footers and preserve line structure.
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

docker compose up -d
```

This starts PostgreSQL, RabbitMQ, the three services, and a `cloudflared` container.

**Local use.** Copy `docker-compose.override.example.yml` to `docker-compose.override.yml` before `docker compose up`. The override publishes the web site on http://localhost and disables the Cloudflare Tunnel so no token is needed. The file is gitignored, so production hosts run the base compose file only.

**Production.** TLS is terminated by Cloudflare. The web container serves plain HTTP on port 80 to the Cloudflare Tunnel over the internal Docker network and publishes no host ports. Create a tunnel in Cloudflare Zero Trust, route its public hostname to `http://web:80`, and set `CLOUDFLARE_TUNNEL_TOKEN` in `.env`; without the token the `cloudflared` container exits on start. If you run without the tunnel, mount a PFX origin certificate at `/app/certs/origin.pfx` and publish ports 80 and 443 on the `web` service instead.

**Useful knobs.**

- `QUARTZ_CRON_SCHEDULE` in `.env` controls when the Processor runs (Quartz format: `second minute hour day month dayOfWeek`; default `0 0 2 * * ?`, daily at 2 AM).
- `docker compose restart processor` triggers a check immediately instead of waiting for the schedule.
- RabbitMQ management UI is at http://localhost:15672. Swagger UI on the web service is only enabled when `ASPNETCORE_ENVIRONMENT=Development` (compose defaults to `Production`).
- On the first run the Processor records every form and sends a single summary email; later runs send one email per changed form.
- Back up the database with `docker compose exec postgres pg_dump -U postgres uscis_forms > backup.sql`.

## Local Development

Requires the .NET 8 SDK, plus PostgreSQL and RabbitMQ (easiest via `docker-compose up -d postgres rabbitmq`).

```bash
dotnet build
dotnet test

# Run individual services
dotnet run --project src/USCISFormTracker.Processor
dotnet run --project src/USCISFormTracker.Emailer
HTTP_PORT=5080 dotnet run --project src/USCISFormTracker.Web  # default port 80 needs root on Linux
```

The web service serves HTTP-only unless a PFX certificate exists at the path given
by `HTTPS_CERT_PATH` (default `/app/certs/origin.pfx`), in which case HTTPS on
port 443 is enabled automatically.

Configuration comes from each service's `appsettings.json` (committed, placeholders only) overridden by environment variables / a local `.env` file. Never commit real credentials — `.env` and `appsettings.*.json` variants are gitignored.

## Notes

- PDF text extraction quality matters for diff quality: the `PdfPigLayoutPdfReader` follows the PDF's letter rendering order, strips footers by Y-position, and groups words into lines so diffs align with the document's real line structure.
- Known issues and planned improvements are tracked in [ISSUES.md](ISSUES.md).
