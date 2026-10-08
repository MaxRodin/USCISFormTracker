# USCIS Form Tracker

Monitors [USCIS immigration forms](https://www.uscis.gov/forms/all-forms) for changes and emails subscribers a line-by-line diff whenever a form's PDF is updated.

USCIS revises its forms without much fanfare, and filing an outdated edition can get an application rejected. This tracker checks every published form daily and reports exactly what changed.

## How It Works

1. A scheduled job (Quartz, daily by default) scrapes the USCIS all-forms page to discover every form's PDF.
2. Each PDF's text is extracted with [PdfPig](https://github.com/UglyToad/PdfPig), using position-based filtering to strip footers and preserve line structure.
3. A SHA-256 hash of the extracted text is compared against the last known hash stored in PostgreSQL.
4. When a hash differs, a line-by-line diff is generated with [DiffPlex](https://github.com/mmanela/diffplex) and the change is recorded.
5. At the end of the run, if anything was added, changed, or removed, one summary email with the diffs is sent to the mailing list via [Mailgun](https://www.mailgun.com/).

## Architecture

Two services backed by PostgreSQL, sharing a set of libraries:

| Project | Role |
|---|---|
| `USCISFormTracker.Processor` | Worker service that runs the scheduled monitoring job: scrape, download, hash, diff, persist, email the summary |
| `USCISFormTracker.Web` | Public site: mailing-list signup (`POST /mailing-list`) and recent changes feed (`GET /changes/recent`) |
| `USCISFormTracker.Core` | Business logic: scraping, PDF text extraction, hashing, diffing |
| `USCISFormTracker.Data` | EF Core persistence (PostgreSQL) and migrations |
| `USCISFormTracker.Email` | Mailgun client: sends the run summary to the mailing list and adds subscribers to it |
| `USCISFormTracker.Formatting` | Formats diffs and run summaries for email and web output |
| `USCISFormTracker.Tests` | xUnit test suite with HTML/PDF fixtures |

## Running with Docker

```bash
cp .env.example .env
# Edit .env — Mailgun credentials are required; the database password
# has a development default you should change for production.

docker compose up -d
```

This starts PostgreSQL, the two services, and a `cloudflared` container.

**Local use.** Copy `docker-compose.override.example.yml` to `docker-compose.override.yml` before `docker compose up`. The override disables the Cloudflare Tunnel so no token is needed; the site is reached at http://127.0.0.1:8080. The file is gitignored, so production hosts run the base compose file only.

**Production.** TLS is terminated by Cloudflare. The web container serves plain HTTP on port 8080 to the Cloudflare Tunnel over the internal Docker network and is published on the host at http://127.0.0.1:8080 only. Create a tunnel in Cloudflare Zero Trust, route its public hostname to `http://web:8080`, and set `CLOUDFLARE_TUNNEL_TOKEN` in `.env`; without the token the `cloudflared` container exits on start.

**Useful knobs.**

- `QUARTZ_CRON_SCHEDULE` in `.env` controls when the Processor runs (Quartz format: `second minute hour day month dayOfWeek`; default `0 0 2 * * ?`, daily at 2 AM).
- `docker compose restart processor` triggers a check immediately instead of waiting for the schedule.
- Swagger UI on the web service is only enabled when `ASPNETCORE_ENVIRONMENT=Development` (compose defaults to `Production`).
- Each run sends at most one summary email, and only when a form was added, changed, or removed. The first run records every form, so it reports them all as new.
- Downloaded PDFs are kept in the `forms_data` volume, mounted at `/app/forms` in the processor container. Files land under `/app/forms/uscis/<form>/`, and the database stores paths relative to that root. If you have PDFs from an older local run in `pdfs/<form>/`, move them to `forms/uscis/<form>/` by hand.
- Back up the database with `docker compose exec postgres pg_dump -U postgres uscis_forms > backup.sql`.

## Local Development

Requires the .NET 8 SDK plus PostgreSQL (easiest via `docker compose up -d postgres`). Both services need the `MAILGUN_*` values from `.env` at startup.

```bash
dotnet build
dotnet test

# Run individual services
dotnet run --project src/USCISFormTracker.Processor
HTTP_PORT=5080 dotnet run --project src/USCISFormTracker.Web  # defaults to port 8080
```

The web service serves plain HTTP only; TLS is terminated by Cloudflare.

Configuration comes from each service's `appsettings.json` (committed, placeholders only) overridden by environment variables / a local `.env` file. Never commit real credentials — `.env` and `appsettings.*.json` variants are gitignored.

## Notes

- PDF text extraction quality matters for diff quality: the `PdfPigLayoutPdfReader` follows the PDF's letter rendering order, strips footers by Y-position, and groups words into lines so diffs align with the document's real line structure.
- Known issues and planned improvements are tracked in [ISSUES.md](ISSUES.md).
