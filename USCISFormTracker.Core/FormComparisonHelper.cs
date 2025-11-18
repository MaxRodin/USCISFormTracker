using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

/// <summary>
/// Helper methods for form comparison operations
/// </summary>
public static class FormComparisonHelper
{
    /// <summary>
    /// Identifies forms that exist in the old set but not in the new set (deleted forms)
    /// </summary>
    /// <param name="oldFormSet">Existing forms (from database)</param>
    /// <param name="newFormSet">Current forms (from website)</param>
    /// <returns>Forms that have been deleted</returns>
    public static IEnumerable<DeletedForm> GetDeletedForms(
        IEnumerable<FormSnapshot> oldFormSet,
        IEnumerable<ScrapedPdf> newFormSet)
    {
        var newFileNames = newFormSet.Select(f => f.FileName).ToHashSet();

        return oldFormSet
            .Where(f => !newFileNames.Contains(f.FileName))
            .Select(f => new DeletedForm
            {
                FileName = f.FileName,
                FormName = f.FormName,
                LastKnownLink = f.FullLink
            });
    }
}
