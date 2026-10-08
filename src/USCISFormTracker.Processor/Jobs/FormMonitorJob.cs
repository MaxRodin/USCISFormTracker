using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using USCISFormTracker.Core;
using USCISFormTracker.Data;
using USCISFormTracker.Email;

namespace USCISFormTracker.Processor.Jobs;

[DisallowConcurrentExecution]
public class FormMonitorJob : IJob
{
    private readonly ILogger<FormMonitorJob> _logger;
    private readonly IFormMonitoringService _monitoringService;
    private readonly IRunSummaryNotifier _notifier;
    private readonly FormTrackerDbContext _dbContext;

    public FormMonitorJob(
        ILogger<FormMonitorJob> logger,
        IFormMonitoringService monitoringService,
        IRunSummaryNotifier notifier,
        FormTrackerDbContext dbContext)
    {
        _logger = logger;
        _monitoringService = monitoringService;
        _notifier = notifier;
        _dbContext = dbContext;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting USCIS Form Tracker job at {Timestamp} UTC", DateTime.UtcNow);

        try
        {
            // Apply pending migrations
            await _dbContext.Database.MigrateAsync();

            // Run the monitoring workflow
            var summary = await _monitoringService.MonitorFormsAsync();

            // Email a summary only when something changed
            if (summary.HasChanges)
            {
                await _notifier.SendAsync(summary);
            }
            else
            {
                _logger.LogInformation(
                    "No form changes detected ({Total} forms checked); skipping summary email",
                    summary.TotalFormsOnWebsite);
            }

            _logger.LogInformation("Form monitoring completed successfully at {Timestamp} UTC", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during form monitoring: {Message}", ex.Message);
            throw;
        }
    }
}
