using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

/// <summary>
/// Orchestrates the complete form monitoring workflow
/// </summary>
public class FormMonitoringService : IFormMonitoringService
{
    private readonly IFormRepository _repository;
    private readonly IFormComparisonService _comparisonService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FormMonitoringService> _logger;

    public FormMonitoringService(
        IFormRepository repository,
        IFormComparisonService comparisonService,
        IHttpClientFactory httpClientFactory,
        ILogger<FormMonitoringService> logger)
    {
        _repository = repository;
        _comparisonService = comparisonService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<FormRunSummary> MonitorFormsAsync()
    {
        var runTime = DateTime.UtcNow;
        _logger.LogInformation("Starting form monitoring at {RunTime}", runTime);

        try
        {
            // Get existing form records from database
            var existingRecords = await _repository.GetAllFormRecordsAsync();
            _logger.LogInformation("Found {Count} existing form records in database", existingRecords.Count);

            // Convert database records to snapshots for Core
            var snapshots = existingRecords.Select(r => new FormSnapshot
            {
                FileName = r.FileName,
                FullLink = r.FullLink,
                FormName = r.FormName,
                Hash = r.Hash,
                ExtractedText = r.ExtractedText,
                LatestPdfPath = r.LatestPdfPath
            }).ToList();

            // Call comparison service to perform the actual comparison
            var httpClient = _httpClientFactory.CreateClient();
            var summary = await _comparisonService.CompareFormsAsync(snapshots, httpClient);

            _logger.LogInformation(
                "Comparison complete: {Added} added, {Changed} changed, {Deleted} deleted",
                summary.AddedForms.Count,
                summary.ChangedForms.Count,
                summary.DeletedForms.Count);

            // Update database with results
            await UpdateDatabaseAsync(summary);

            _logger.LogInformation("Form monitoring completed successfully at {Timestamp} UTC", DateTime.UtcNow);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during form monitoring: {Message}", ex.Message);
            throw;
        }
    }

    private async Task UpdateDatabaseAsync(FormRunSummary summary)
    {
        // Add new forms
        foreach (var addedForm in summary.AddedForms)
        {
            var record = new PdfFormRecord
            {
                FileName = addedForm.FileName,
                FullLink = addedForm.FullLink,
                FormName = addedForm.FormName,
                Hash = addedForm.Hash,
                ExtractedText = addedForm.ExtractedText,
                LatestPdfPath = addedForm.PdfPath,
                LastChecked = summary.RunTime
            };
            await _repository.AddFormRecordAsync(record);
        }

        // Update changed forms
        foreach (var changedForm in summary.ChangedForms)
        {
            var existingRecord = await _repository.GetFormRecordByLinkAsync(changedForm.FileName);
            if (existingRecord != null)
            {
                // Save change history
                var change = new PdfFormChange
                {
                    FileName = changedForm.FileName,
                    FullLink = changedForm.FullLink,
                    FormName = changedForm.FormName,
                    OldHash = changedForm.OldHash,
                    NewHash = changedForm.NewHash,
                    OldPdfPath = changedForm.OldPdfPath,
                    NewPdfPath = changedForm.NewPdfPath,
                    DiffLinesSerialized = JsonSerializer.Serialize(changedForm.Diff),
                    DetectedChangeTime = summary.RunTime
                };
                await _repository.AddFormChangeAsync(change);

                // Update record with new hash, text, and PDF path
                existingRecord.Hash = changedForm.NewHash;
                existingRecord.ExtractedText = changedForm.NewText;
                existingRecord.LatestPdfPath = changedForm.NewPdfPath;
                existingRecord.LastChecked = summary.RunTime;
                await _repository.UpdateFormRecordAsync(existingRecord);
            }
        }

        // Handle deleted forms (soft delete)
        foreach (var deletedForm in summary.DeletedForms)
        {
            var existingRecord = await _repository.GetFormRecordByLinkIncludingDeletedAsync(deletedForm.FileName);
            if (existingRecord != null && existingRecord.IsActive)
            {
                existingRecord.IsActive = false;
                existingRecord.DeletedAt = summary.RunTime;
                await _repository.UpdateFormRecordAsync(existingRecord);
                _logger.LogInformation("Form marked as deleted in database: {FormName} ({FileName})", deletedForm.FormName, deletedForm.FileName);
            }
        }
    }
}
