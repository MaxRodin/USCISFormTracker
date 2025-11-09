# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

USCIS Form Change Tracker - monitors USCIS immigration forms for changes by computing hashes, comparing them, and sending email notifications with diffs when changes are detected.

## Architecture

**Interface-based design** with the following key components:

- **IHasher** (Sha256Hasher): Computes SHA256 hashes of PDF text content
- **IPdfReader** (PdfPigReader): Extracts text from PDF files using PdfPig library
- **IDiffer**: Generates line-by-line diffs between old and new PDF text
- **IWebPdfGetter**: Scrapes USCIS website to discover PDF form links
- **IEmailService/IEmailSender**: Sends change notifications via Mailgun
- **Repository layer**: Manages persistence to SQLite database using Entity Framework Core

**Data flow:**
1. IWebPdfGetter retrieves PDF links from USCIS website
2. IPdfReader extracts text from PDFs
3. IHasher computes hash of extracted text
4. Compare hash with stored PdfFormRecord
5. If different: IDiffer generates diff, store PdfFormChange, send email via IEmailService
6. Update PdfFormRecord with new hash

**Models:**
- `PdfFormRecord`: Stores form metadata and current hash
- `PdfFormChange`: Records detected changes with diff and timestamps
- `DiffLines`: Contains added/deleted/modified lines from diff
- `ScrapedPdf`: (purpose TBD)

## Development

**Build:**
```bash
dotnet build
```

**Run (once console app is created):**
```bash
dotnet run --project USCISFormTracker.ConsoleApp
```

**Test:**
```bash
dotnet test
```

**Execution model:** Console application designed to be scheduled externally (cron/Task Scheduler), not a continuously running service.

## Configuration

Email configuration and sensitive data should be stored in `appsettings.json` (gitignored except template).

Downloaded PDFs are stored in `forms/` directory (gitignored).
