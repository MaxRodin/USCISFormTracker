using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

/// <summary>
/// Pure comparison service - no database, no message bus, just form comparison logic
/// </summary>
public interface IFormComparisonService
{
    /// <summary>
    /// Compares current forms on USCIS website against existing records
    /// </summary>
    /// <param name="existingRecords">Current form records from database</param>
    /// <returns>Summary of added, changed, and deleted forms</returns>
    Task<FormRunSummary> CompareFormsAsync(IEnumerable<PdfFormRecord> existingRecords);
}
