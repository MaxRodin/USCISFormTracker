using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

/// <summary>
/// Orchestrates the complete form monitoring workflow
/// </summary>
public interface IFormMonitoringService
{
    /// <summary>
    /// Monitors USCIS forms for changes
    /// </summary>
    /// <returns>Summary of the monitoring run including new, changed, and deleted forms</returns>
    Task<FormRunSummary> MonitorFormsAsync();
}
