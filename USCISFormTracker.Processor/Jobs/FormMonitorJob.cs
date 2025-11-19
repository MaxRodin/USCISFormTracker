using MassTransit;
using Microsoft.EntityFrameworkCore;
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
    private readonly IFormMonitoringService _monitoringService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly FormTrackerDbContext _dbContext;
    private readonly IFormRepository _repository;

    public FormMonitorJob(
        ILogger<FormMonitorJob> logger,
        IFormMonitoringService monitoringService,
        IPublishEndpoint publishEndpoint,
        FormTrackerDbContext dbContext,
        IFormRepository repository)
    {
        _logger = logger;
        _monitoringService = monitoringService;
        _publishEndpoint = publishEndpoint;
        _dbContext = dbContext;
        _repository = repository;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting USCIS Form Tracker job at {Timestamp} UTC", DateTime.UtcNow);

        try
        {
            // Apply pending migrations
            await _dbContext.Database.MigrateAsync();

            // Execute monitoring (orchestration handled by Core)
            var summary = await _monitoringService.MonitorFormsAsync();

            // Publish results
            await PublishAggregateSummaryAsync(summary);

            _logger.LogInformation("Form monitoring completed successfully at {Timestamp} UTC", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during form monitoring: {Message}", ex.Message);
            throw;
        }
    }

    private async Task PublishAggregateSummaryAsync(FormRunSummary summary)
    {
        _logger.LogInformation(
            "Publishing aggregate summary: {NewCount} new, {ChangedCount} changed, {DeletedCount} deleted",
            summary.AddedForms.Count,
            summary.ChangedForms.Count,
            summary.DeletedForms.Count);

        var message = new RunSummaryMessage
        {
            RunTime = summary.RunTime,
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

        await _publishEndpoint.Publish(message);
        _logger.LogInformation("Aggregate summary message published");
    }
}
