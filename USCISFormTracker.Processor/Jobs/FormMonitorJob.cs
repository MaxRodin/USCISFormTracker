using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using USCISFormTracker.Core;
using USCISFormTracker.Core.Models;
using USCISFormTracker.Data;
using USCISFormTracker.Dto;

namespace USCISFormTracker.Processor.Jobs;

[DisallowConcurrentExecution]
public class FormMonitorJob : IJob
{
    private readonly ILogger<FormMonitorJob> _logger;
    private readonly IServiceProvider _serviceProvider;

    public FormMonitorJob(
        ILogger<FormMonitorJob> logger,
        IServiceProvider _serviceProvider)
    {
        _logger = logger;
        this._serviceProvider = _serviceProvider;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting USCIS Form Tracker job at {Timestamp} UTC", DateTime.UtcNow);

        try
        {
            using var scope = _serviceProvider.CreateScope();

            // Get services
            var dbContext = scope.ServiceProvider.GetRequiredService<FormTrackerDbContext>();
            var repository = scope.ServiceProvider.GetRequiredService<IFormRepository>();
            var comparisonService = scope.ServiceProvider.GetRequiredService<IFormComparisonService>();
            var httpClient = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            // Ensure database is created
            await dbContext.Database.EnsureCreatedAsync();

            // Get existing form records from database
            var existingRecords = await repository.GetAllFormRecordsAsync();
            _logger.LogInformation("Found {Count} existing form records in database", existingRecords.Count);

            // Determine if this is first run (empty database)
            bool isFirstRun = existingRecords.Count == 0;
            if (isFirstRun)
            {
                _logger.LogInformation("First run detected - will send aggregate summary instead of individual notifications");
            }

            // Convert database records to snapshots for Core
            var snapshots = existingRecords.Select(r => new FormSnapshot
            {
                FileName = r.FileName,
                FullLink = r.FullLink,
                FormName = r.FormName,
                Hash = r.Hash,
                ExtractedText = r.ExtractedText
            }).ToList();

            // Call Core to perform comparison
            var summary = await comparisonService.CompareFormsAsync(snapshots, httpClient);

            _logger.LogInformation(
                "Comparison complete: {Added} added, {Changed} changed, {Deleted} deleted",
                summary.AddedForms.Count,
                summary.ChangedForms.Count,
                summary.DeletedForms.Count);

            // Update database with results
            await UpdateDatabaseAsync(repository, summary);

            // Always publish aggregate summary (whether first run or not)
            await PublishAggregateSummaryAsync(publishEndpoint, summary, isFirstRun);

            _logger.LogInformation("Form monitoring completed successfully at {Timestamp} UTC", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during form monitoring: {Message}", ex.Message);
            throw;
        }
    }

    private async Task UpdateDatabaseAsync(IFormRepository repository, FormRunSummary summary)
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
                LastChecked = summary.RunTime
            };
            await repository.AddFormRecordAsync(record);
        }

        // Update changed forms
        foreach (var changedForm in summary.ChangedForms)
        {
            var existingRecord = await repository.GetFormRecordByLinkAsync(changedForm.FileName);
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
                    DiffLinesSerialized = JsonSerializer.Serialize(changedForm.Diff),
                    DetectedChangeTime = summary.RunTime
                };
                await repository.AddFormChangeAsync(change);

                // Update record with new hash and text
                existingRecord.Hash = changedForm.NewHash;
                existingRecord.ExtractedText = changedForm.NewText;
                existingRecord.LastChecked = summary.RunTime;
                await repository.UpdateFormRecordAsync(existingRecord);
            }
        }

        // Handle deleted forms (soft delete)
        foreach (var deletedForm in summary.DeletedForms)
        {
            var existingRecord = await repository.GetFormRecordByLinkIncludingDeletedAsync(deletedForm.FileName);
            if (existingRecord != null && existingRecord.IsActive)
            {
                existingRecord.IsActive = false;
                existingRecord.DeletedAt = summary.RunTime;
                await repository.UpdateFormRecordAsync(existingRecord);
                _logger.LogInformation("Form marked as deleted in database: {FormName} ({FileName})", deletedForm.FormName, deletedForm.FileName);
            }
        }
    }

    private async Task PublishAggregateSummaryAsync(IPublishEndpoint publishEndpoint, FormRunSummary summary, bool isFirstRun)
    {
        _logger.LogInformation(
            "Publishing aggregate summary: {NewCount} new, {ChangedCount} changed, {DeletedCount} deleted",
            summary.AddedForms.Count,
            summary.ChangedForms.Count,
            summary.DeletedForms.Count);

        var message = new RunSummaryMessage
        {
            RunTime = summary.RunTime,
            IsFirstRun = isFirstRun,
            TotalFormsOnWebsite = summary.TotalFormsOnWebsite,
            NewFormsCount = summary.AddedForms.Count,
            ChangedFormsCount = summary.ChangedForms.Count,
            DeletedFormsCount = summary.DeletedForms.Count,
            NewForms = summary.AddedForms.Select(f => new FormSummaryItem
            {
                FileName = f.FileName,
                FormName = f.FormName,
                FullLink = f.FullLink
            }).ToList(),
            ChangedForms = summary.ChangedForms.Select(f => new FormSummaryItem
            {
                FileName = f.FileName,
                FormName = f.FormName,
                FullLink = f.FullLink,
                AddedLines = f.Diff.AddedLines,
                DeletedLines = f.Diff.DeletedLines,
                ModifiedLines = f.Diff.ModifiedLines
            }).ToList(),
            DeletedForms = summary.DeletedForms.Select(f => new FormSummaryItem
            {
                FileName = f.FileName,
                FormName = f.FormName,
                FullLink = f.LastKnownLink
            }).ToList()
        };

        await publishEndpoint.Publish(message);
        _logger.LogInformation("Aggregate summary message published");
    }
}
