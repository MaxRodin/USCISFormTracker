# USCIS Form Tracker - Known Issues and Improvements

This document tracks issues identified during codebase review, organized by priority.

---

## Critical Issues

### Issue #3: Deleted Forms Not Handled in Database
**Status**: Open
**Priority**: High
**Location**: `USCISFormTracker.Processor/Jobs/FormMonitorJob.cs:135-141`

**Problem**: When forms are removed from the USCIS website, they are detected and reported in emails, but remain in the database indefinitely. This causes data integrity issues over time.

**Current Code**:
```csharp
// TODO: Handle deleted forms (mark as inactive or remove)
foreach (var deletedForm in summary.DeletedForms)
{
    _logger.LogWarning("Form deleted but not removed from database: {FormName}", deletedForm.FormName);
}
```

**Impact**: Medium-High - Data accumulates incorrectly, database grows with stale records

**Recommended Solution**: Implement soft delete pattern
```csharp
// 1. Add to PdfFormRecord.cs:
public bool IsActive { get; set; } = true;
public DateTime? DeletedAt { get; set; }

// 2. Update FormMonitorJob.cs:
foreach (var deletedForm in summary.DeletedForms)
{
    var existingRecord = await repository.GetFormRecordByLinkAsync(deletedForm.FileName);
    if (existingRecord != null)
    {
        existingRecord.IsActive = false;
        existingRecord.DeletedAt = summary.RunTime;
        await repository.UpdateFormRecordAsync(existingRecord);
    }
}

// 3. Update queries to filter: .Where(f => f.IsActive)
```

---

## Important Improvements

### Issue #4: Missing Resilience Patterns
**Status**: Open
**Priority**: Medium-High
**Location**: All HTTP calls and database operations

**Problem**: No retry logic, circuit breakers, or timeout policies. Transient failures (network hiccups, temporary server issues) will cause the entire job to fail.

**Impact**: Medium - Production reliability issues

**Recommended Solution**: Add Polly library
```bash
dotnet add USCISFormTracker.Core package Microsoft.Extensions.Http.Polly
```

```csharp
// In ServiceExtensions.cs
services.AddHttpClient<IWebPdfGetter, UscisWebPdfGetter>()
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                // Log retry attempts
            }))
    .AddTransientHttpErrorPolicy(policy =>
        policy.CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromMinutes(1)));
```

---

### Issue #5: Database Connection String Security
**Status**: Open
**Priority**: Low-Medium
**Location**: `USCISFormTracker.Processor/ServiceExtensions.cs:12-22`

**Problem**: Database credentials built from environment variables could be exposed in error messages and logs.

**Impact**: Low-Medium - Potential credential exposure

**Recommended Solution**:
1. Use NpgsqlConnectionStringBuilder which hides passwords in error messages
2. Ensure logging configuration doesn't log connection strings
3. Consider using secrets management (Azure Key Vault, AWS Secrets Manager, etc.)

---

### Issue #6: No Configuration Validation at Startup
**Status**: Open
**Priority**: Medium
**Location**: All `Program.cs` files

**Problem**: Missing configuration values cause runtime failures with unclear error messages. Makes troubleshooting difficult.

**Impact**: Medium - Poor developer experience, difficult debugging

**Recommended Solution**:
```csharp
// Add to Processor/Program.cs after builder.Build()
var requiredEnvVars = new Dictionary<string, string?>
{
    ["DATABASE_HOST"] = Environment.GetEnvironmentVariable("DATABASE_HOST"),
    ["DATABASE_PASSWORD"] = Environment.GetEnvironmentVariable("DATABASE_PASSWORD"),
    ["DATABASE_NAME"] = Environment.GetEnvironmentVariable("DATABASE_NAME"),
    ["RabbitMQ:Host"] = builder.Configuration["RabbitMQ:Host"],
};

var missingConfigs = requiredEnvVars
    .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
    .Select(kvp => kvp.Key)
    .ToList();

if (missingConfigs.Any())
{
    throw new InvalidOperationException(
        $"Missing required configuration: {string.Join(", ", missingConfigs)}. " +
        "Please check your .env file and environment variables.");
}
```

---

### Issue #7: Email Sending Has No Error Recovery
**Status**: Open
**Priority**: Medium
**Location**: `USCISFormTracker.Emailer/Consumers/RunSummaryConsumer.cs`

**Problem**: If Mailgun API fails, the email notification is lost forever. No retry mechanism.

**Impact**: Medium - Missed notifications on transient failures

**Recommended Solution**: Configure MassTransit retry policy
```csharp
// In Emailer/Program.cs
x.AddConsumer<RunSummaryConsumer>(cfg =>
{
    cfg.UseMessageRetry(r =>
    {
        r.Interval(3, TimeSpan.FromMinutes(5));
        r.Ignore<ArgumentException>(); // Don't retry on validation errors
    });
});

// Configure dead-letter queue for failed messages
cfg.ReceiveEndpoint("run-summary-error", e =>
{
    e.ConfigureConsumer<RunSummaryConsumer>(context);
});
```

---

### Issue #8: Large Text Storage in Database
**Status**: Open
**Priority**: Low-Medium
**Location**: `USCISFormTracker.Processor/Models/PdfFormRecord.cs:26`

**Problem**: `ExtractedText` stored as unlimited string. Some USCIS forms could be very large (100+ pages), causing database bloat.

**Impact**: Low-Medium - Database performance degradation over time

**Recommended Solutions** (choose one):
1. **Add column length limit and compression**:
   ```csharp
   entity.Property(e => e.ExtractedText)
       .HasMaxLength(100000)
       .IsRequired();
   ```

2. **Store in blob storage** (S3, Azure Blob, MinIO):
   - Keep only hash and metadata in database
   - Store full text in object storage
   - Reference by key

3. **Monitor and decide**: Add metrics to track typical text sizes first

---

### Issue #9: Missing Health Checks
**Status**: Open
**Priority**: Medium
**Location**: All service `Program.cs` files

**Problem**: No health check endpoints for monitoring service health in production.

**Impact**: Medium - Difficult to monitor, no early warning for issues

**Recommended Solution**:
```bash
dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks
dotnet add package AspNetCore.HealthChecks.NpgSql
dotnet add package AspNetCore.HealthChecks.RabbitMQ
```

```csharp
// In Processor/Program.cs
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "database")
    .AddRabbitMQ(rabbitConnectionString, name: "rabbitmq");

// Add endpoint
app.MapHealthChecks("/health");
```

---

### Issue #10: No Structured Logging
**Status**: Open
**Priority**: Low-Medium
**Location**: Throughout codebase

**Problem**: String interpolation used instead of structured logging. Makes log querying and analysis difficult.

**Impact**: Low-Medium - Reduced observability

**Current Examples**:
```csharp
_logger.LogInformation($"Found {count} PDFs");
_logger.LogError($"Error processing {fileName}");
```

**Recommended Pattern**:
```csharp
_logger.LogInformation("Found {PdfCount} PDFs on USCIS website", count);
_logger.LogError("Error processing form {FileName}: {ErrorMessage}", fileName, ex.Message);
```

**Benefits**:
- Queryable structured data in log aggregators (Seq, ELK, Datadog)
- Better performance (no string concatenation)
- Type safety

---

## Nice-to-Have Enhancements

### Issue #11: No Rate Limiting on USCIS Website
**Status**: Open
**Priority**: Low
**Location**: `USCISFormTracker.Core/FormComparisonService.cs`

**Problem**: Downloads all PDFs sequentially with no delays. Could overwhelm USCIS servers or trigger rate limiting.

**Impact**: Low - Risk of IP blocking, being a bad citizen

**Recommended Solution**: Add concurrency limiting
```csharp
private static readonly SemaphoreSlim _semaphore = new(3, 3); // Max 3 concurrent

// In ProcessFormAsync, before HTTP call:
await _semaphore.WaitAsync();
try
{
    using var response = await httpClient.GetAsync(scrapedPdf.FullLink);
    // ... rest of processing
}
finally
{
    _semaphore.Release();
}
```

Or add delay between requests:
```csharp
await Task.Delay(TimeSpan.FromMilliseconds(100)); // 100ms between requests
```

---

### Issue #12: Missing Metrics and Observability
**Status**: Open
**Priority**: Low
**Location**: Throughout codebase

**Problem**: No metrics collection for monitoring system behavior and trends.

**Impact**: Low - Limited visibility into system performance

**Recommended Metrics**:
```csharp
// Using App.Metrics or Prometheus.NET
- forms_checked_total (counter)
- forms_new_total (counter)
- forms_changed_total (counter)
- forms_deleted_total (counter)
- job_duration_seconds (histogram)
- http_request_duration_seconds (histogram)
- http_request_errors_total (counter)
- pdf_download_bytes (histogram)
- database_operation_duration_seconds (histogram)
```

---

### Issue #13: Test Coverage Gaps
**Status**: Open
**Priority**: Low
**Location**: `USCISFormTracker.Tests/`

**Current Coverage**: 27 tests covering:
- PDF text extraction ✓
- Hash computation ✓
- Diff generation ✓
- Web scraping ✓

**Missing Test Coverage**:
- [ ] FormComparisonService integration tests
- [ ] FormMonitorJob workflow tests
- [ ] EmailContentBuilder formatting tests
- [ ] Repository layer tests
- [ ] Error scenarios:
  - [ ] Network failures during PDF download
  - [ ] Malformed HTML from USCIS website
  - [ ] Corrupted PDF files
  - [ ] Database connection failures
  - [ ] RabbitMQ connection failures
  - [ ] Mailgun API failures

**Recommended Additions**:
1. Add integration tests for FormComparisonService
2. Test error handling paths
3. Test edge cases (empty PDFs, huge PDFs, non-English text)
4. Add performance tests for large form sets

---

### Issue #14: No PDF Download Validation
**Status**: Open
**Priority**: Low
**Location**: `USCISFormTracker.Core/FormComparisonService.cs:97-108`

**Problem**: Downloads any content from PDF URLs without validating content type or size.

**Impact**: Low - Could process non-PDF files or extremely large files

**Recommended Solution**:
```csharp
using var response = await httpClient.GetAsync(scrapedPdf.FullLink);
if (!response.IsSuccessStatusCode)
{
    _logger.LogWarning("Failed to download PDF from {Link}: {StatusCode}",
        scrapedPdf.FullLink, response.StatusCode);
    return;
}

// Validate content type
if (response.Content.Headers.ContentType?.MediaType != "application/pdf")
{
    _logger.LogWarning("Expected PDF but got {ContentType} from {Link}",
        response.Content.Headers.ContentType?.MediaType, scrapedPdf.FullLink);
    return;
}

// Validate size (e.g., 50MB limit)
var contentLength = response.Content.Headers.ContentLength;
if (contentLength.HasValue && contentLength.Value > 50 * 1024 * 1024)
{
    _logger.LogWarning("PDF too large ({SizeBytes} bytes) from {Link}, skipping",
        contentLength.Value, scrapedPdf.FullLink);
    return;
}

using var stream = await response.Content.ReadAsStreamAsync();
// ... continue processing
```

---

### Issue #15: Email Templates Hardcoded in C#
**Status**: Open
**Priority**: Low
**Location**: `USCISFormTracker.Emailer/Services/EmailContentBuilder.cs`

**Problem**: HTML and text email templates are hardcoded in C# with StringBuilder. Difficult to edit and preview.

**Impact**: Low - Harder to maintain and customize emails

**Recommended Solutions**:
1. **Razor templates** (RazorLight library):
   ```csharp
   var engine = new RazorLightEngineBuilder()
       .UseFileSystemProject(Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplates"))
       .Build();

   var html = await engine.CompileRenderAsync("RunSummary.cshtml", summary);
   ```

2. **Handlebars/Scriban** (simpler, no Razor complexity)

3. **External template files** (minimum change):
   - Move HTML/text to `.html` and `.txt` files
   - Load at runtime
   - Use simple token replacement

---

### Issue #16: No Database Indexes for Common Queries
**Status**: Open
**Priority**: Low
**Location**: `USCISFormTracker.Processor/Data/FormTrackerDbContext.cs`

**Current State**: Only unique index on `FileName`

**Problem**: Queries by date or form name will be slow as data grows.

**Impact**: Low now, Medium later - Performance degradation with large datasets

**Recommended Solution**:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<PdfFormRecord>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.FileName).IsUnique();
        entity.HasIndex(e => e.LastChecked); // For date range queries
        entity.HasIndex(e => e.FormName);    // For form-specific lookups
        // ... other configs
    });

    modelBuilder.Entity<PdfFormChange>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.FileName);           // For history lookup
        entity.HasIndex(e => e.DetectedChangeTime); // For date filtering
        entity.HasIndex(new[] { e.FileName, e.DetectedChangeTime }); // Composite for common query
        // ... other configs
    });
}
```

---

## Missing Features

These are features that don't exist but would be valuable:

### Feature #17: Manual Form Check Trigger
**Priority**: Low
**Description**: No way to manually trigger a form check outside of the cron schedule.

**Use Case**: User wants to test the system or force a check after configuration changes.

**Recommended Solution**: Add HTTP endpoint or CLI command to trigger FormMonitorJob on demand.

---

### Feature #18: Web Dashboard
**Priority**: Low
**Description**: No UI to view form history or change trends.

**Potential Features**:
- List all monitored forms
- View change history for a specific form
- Visual diff viewer
- Trend charts (changes over time)
- Form statistics

---

### Feature #19: Configurable Email Recipients
**Priority**: Low
**Description**: Currently hardcoded to send to mailing list only.

**Potential Enhancement**:
- Multiple recipient groups (admins, subscribers, specific forms)
- Per-user form subscriptions
- Email preferences (all changes vs. only new forms)

---

### Feature #20: Form Filtering
**Priority**: Low
**Description**: No way to monitor only specific forms.

**Use Case**: User only cares about I-485, I-140, and I-130 forms, not all 200+ forms.

**Recommended Solution**: Configuration for form whitelist/blacklist.

---

### Feature #21: Change History API
**Priority**: Low
**Description**: No way to query historical changes programmatically.

**Use Case**: External systems want to integrate with form change data.

**Recommended Solution**: Add REST API endpoints to query PdfFormChange records.

---

### Feature #22: Diff Visualization
**Priority**: Low
**Description**: Email diff is text-only, could benefit from side-by-side view.

**Potential Enhancement**:
- HTML side-by-side diff view
- Syntax highlighting
- Collapse unchanged sections

---

### Feature #23: Database Cleanup Job
**Priority**: Low
**Description**: No retention policy or cleanup for old change records.

**Impact**: Database will grow indefinitely.

**Recommended Solution**:
- Configurable retention period (e.g., keep last 12 months)
- Scheduled cleanup job (Quartz or separate background task)
- Archive to cheaper storage before deletion

---

## Completed Issues

### ~~Issue #1: Blocking Async Calls in UscisWebPdfGetter~~ ✓
**Status**: Fixed
**Fixed In**: Commit [pending]
**Solution**: Made IWebPdfGetter.GetPdfLinksAsync() async, updated all callers

### ~~Issue #2: Silent Exception Swallowing~~ ✓
**Status**: Fixed
**Fixed In**: Commit [pending]
**Solution**: Added ILogger to UscisWebPdfGetter, replaced empty catch with logging

---

## Notes

- This document should be updated as issues are addressed
- Priority levels may change as product needs evolve
- Some "nice-to-have" items may become critical for production deployment
