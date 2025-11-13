using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using USCISFormTracker.Core;
using USCISFormTracker.Core.Models;
using USCISFormTracker.Dto;
using USCISFormTracker.Processor.Data;
using USCISFormTracker.Processor.Models;

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

            // Publish messages based on first run or not
            if (isFirstRun && summary.AddedForms.Count > 0)
            {
                await PublishAggregateSummaryAsync(publishEndpoint, summary);
            }
            else
            {
                await PublishIndividualMessagesAsync(publishEndpoint, summary);
            }

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

        // TODO: Handle deleted forms (mark as inactive or remove)
        // For now, just log them
        foreach (var deletedForm in summary.DeletedForms)
        {
            _logger.LogWarning("Form deleted but not removed from database: {FormName}", deletedForm.FormName);
            // Could implement soft delete or removal here
        }
    }

    private async Task PublishAggregateSummaryAsync(IPublishEndpoint publishEndpoint, FormRunSummary summary)
    {
        _logger.LogInformation(
            "Publishing aggregate summary: {NewCount} new, {ChangedCount} changed, {DeletedCount} deleted",
            summary.AddedForms.Count,
            summary.ChangedForms.Count,
            summary.DeletedForms.Count);

        var message = new RunSummaryMessage
        {
            RunTime = summary.RunTime,
            IsFirstRun = true,
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
                FullLink = f.FullLink
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

    private async Task PublishIndividualMessagesAsync(IPublishEndpoint publishEndpoint, FormRunSummary summary)
    {
        // Publish FormAddedMessage for new forms
        foreach (var addedForm in summary.AddedForms)
        {
            await publishEndpoint.Publish(new FormAddedMessage
            {
                FileName = addedForm.FileName,
                FullLink = addedForm.FullLink,
                FormName = addedForm.FormName,
                Hash = addedForm.Hash,
                DiscoveredTime = summary.RunTime
            });
            _logger.LogInformation("Published FormAddedMessage for {FormName}", addedForm.FormName);
        }

        // Publish FormChangeDetectedMessage for changed forms
        foreach (var changedForm in summary.ChangedForms)
        {
            await publishEndpoint.Publish(new FormChangeDetectedMessage
            {
                FileName = changedForm.FileName,
                FullLink = changedForm.FullLink,
                FormName = changedForm.FormName,
                OldHash = changedForm.OldHash,
                NewHash = changedForm.NewHash,
                DetectedChangeTime = summary.RunTime,
                AddedLines = changedForm.Diff.AddedLines,
                DeletedLines = changedForm.Diff.DeletedLines,
                ModifiedLines = changedForm.Diff.ModifiedLines
            });
            _logger.LogInformation("Published FormChangeDetectedMessage for {FormName}", changedForm.FormName);
        }

        // Publish FormDeletedMessage for deleted forms
        foreach (var deletedForm in summary.DeletedForms)
        {
            await publishEndpoint.Publish(new FormDeletedMessage
            {
                FormName = deletedForm.FormName,
                Link = deletedForm.LastKnownLink,
                LastSeen = summary.RunTime
            });
            _logger.LogInformation("Published FormDeletedMessage for {FormName}", deletedForm.FormName);
        }
    }
}
