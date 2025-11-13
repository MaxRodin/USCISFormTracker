using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

/// <summary>
/// Pure comparison service - no database, no message bus, just form comparison logic
/// </summary>
public interface IFormComparisonService
{
    /// <summary>
    /// Compares current forms on USCIS website against existing snapshots
    /// </summary>
    /// <param name="existingSnapshots">Current form snapshots from database</param>
    /// <param name="httpClient">HTTP client for downloading PDFs</param>
    /// <returns>Summary of added, changed, and deleted forms</returns>
    Task<FormRunSummary> CompareFormsAsync(
        IEnumerable<FormSnapshot> existingSnapshots,
        HttpClient httpClient);
}
