using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Email;

/// <summary>
/// Emails the mailing list a summary of a monitoring run.
/// </summary>
public interface IRunSummaryNotifier
{
    /// <summary>
    /// Sends one summary email to the mailing list. Does nothing when the run had no changes.
    /// </summary>
    Task SendAsync(FormRunSummary summary);
}
