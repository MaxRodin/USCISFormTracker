# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

USCIS Form Change Tracker - monitors USCIS immigration forms for changes by computing hashes, comparing them, and sending email notifications with diffs when changes are detected.

## Architecture

**Interface-based design** with the following key components:

- **IHasher** (Sha256Hasher): Computes SHA256 hashes of PDF text content
- **IPdfReader** (ImprovedPdfPigReader): Extracts text from PDF files using PdfPig library with intelligent header/footer filtering and line break preservation
- **IDiffer** (DiffPlexDiffer): Generates line-by-line diffs between old and new PDF text using DiffPlex library (Myers' diff algorithm)
- **IWebPdfGetter** (UscisWebPdfGetter): Scrapes USCIS website to discover PDF form links
- **IEmailService/IEmailSender**: Sends change notifications via Mailgun
- **Repository layer**: Manages persistence to SQLite database using Entity Framework Core

**Data flow:**
1. IWebPdfGetter retrieves PDF links from USCIS website
2. IPdfReader extracts text from PDFs
3. IHasher computes hash of extracted text
4. Compare hash with stored PdfFormRecord
5. If different: IDiffer generates diff, store PdfFormChange, send email via IEmailService
6. Update PdfFormRecord with new hash

**IWebPdfGetter Implementation Details:**

The USCIS website has a two-tier structure for accessing form PDFs:

1. **All-Forms Page** (https://www.uscis.gov/forms/all-forms): This is the "root directory" containing links to individual form detail pages. Links have the pattern:
   ```html
   <a href="/i-694" class="link link--form-title">I-694 | Notice of Appeal...</a>
   ```

2. **Form Detail Pages** (e.g., https://www.uscis.gov/i-694): Each detail page contains the actual PDF download link.

The `UscisWebPdfGetter` implementation:
1. Fetches the all-forms page and extracts all form detail links (XPath: `//a[@class='link link--form-title']`)
2. For each detail link, fetches that page and extracts the PDF link (any `<a>` tag with `.pdf` extension)
3. Returns all discovered PDF URLs

Example HTML snippet from all-forms page:
```html
<a href="/i-694" class="link link--form-title">I-694 | Notice of Appeal</a>
<a href="/ar-11" class="link link--form-title">AR-11 | Alien's Change of Address</a>
```

**Models:**
- `PdfFormRecord`: Stores form metadata and current hash
  - `FileName`: PDF filename only (e.g., "i-751.pdf") - unique identifier
  - `FullLink`: Complete URL to the PDF
  - `FormName`: Human-readable name extracted from filename
  - `Hash`: SHA256 hash of PDF text content
  - `LastChecked`: Timestamp of last check
- `PdfFormChange`: Records detected changes with diff and timestamps
  - `FileName`: PDF filename at time of change
  - `FullLink`: Complete URL at time of change detection
  - `FormName`: Human-readable name
  - `OldHash`, `NewHash`: Hashes before and after change
  - `DiffLinesSerialized`: JSON-serialized DiffLines
  - `DetectedChangeTime`: When change was detected
- `PdfLinkInfo`: DTO returned by IWebPdfGetter
  - `FileName`: PDF filename (e.g., "i-751.pdf")
  - `FullLink`: Complete URL to the PDF
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

## Testing

The test project (`USCISFormTracker.Tests`) uses xUnit and Moq with organized test data:

**Test Structure:**
```
USCISFormTracker.Tests/
├── TestData/
│   ├── Html/           # HTML fixtures for web scraping tests
│   │   ├── all-forms-snippet.html
│   │   ├── i-694-detail-example.html
│   │   └── i-751-detail.html
│   └── Pdf/            # PDF fixtures for diff testing
│       ├── PdfTest_First.pdf
│       └── PdfTest_Second.pdf
├── TestHelpers/
│   ├── MockHttpMessageHandler.cs  # Mock HTTP responses
│   └── TestDataLoader.cs          # Load test fixtures
├── UscisWebPdfGetterTests.cs      # Web scraping tests
├── FormChangeDetectionTests.cs    # End-to-end workflow tests
└── DiffInspectionTests.cs         # Detailed diff output inspection
```

**Key Test Scenarios:**
- All-forms page scraping and form detail link extraction
- Detail page navigation and PDF link extraction
- PDF text extraction and hash computation
- Change detection between PDF versions
- DiffLines generation using DiffPlex
- Full monitoring workflow simulation with mock HTTP
- PDF structure analysis and extraction quality tests

## PDF Text Extraction

The `ImprovedPdfPigReader` addresses common PDF extraction issues:

**Problems with basic `page.Text` extraction:**
1. Headers/footers mixed into content (e.g., "OriginalHeader", date stamps)
2. Sentences on different Y positions concatenated with spaces instead of line breaks
3. No semantic understanding of document structure

**ImprovedPdfPigReader solution:**
1. **Header/Footer Filtering**: Uses absolute Y-position thresholds based on page dimensions
   - Headers: Top 50 points (~0.7 inches from top)
   - Footers: Bottom 80 points (~1.1 inches from bottom)
2. **Line Break Preservation**: Groups words by Y position (within 3-point tolerance) to maintain original line structure
3. **Content-only extraction**: Filters out peripheral text, focusing on document body

**Example transformation:**
```
Before (PdfPigReader):
OriginalHeader    10/11/2025    This line is static. This line will change. We are going to delete this line.

After (ImprovedPdfPigReader):
This line is static.
This line will change.
We are going to delete this line.
```

This produces cleaner diffs with better semantic meaning.

## Configuration

Email configuration and sensitive data should be stored in `appsettings.json` (gitignored except template).

Downloaded PDFs are stored in `forms/` directory (gitignored).
