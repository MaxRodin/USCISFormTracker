using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public interface IFormRepository
{
    Task<PdfFormRecord?> GetFormRecordByLinkAsync(string link);
    Task<PdfFormRecord?> GetFormRecordByLinkIncludingDeletedAsync(string link);
    Task<List<PdfFormRecord>> GetAllFormRecordsAsync();
    Task AddFormRecordAsync(PdfFormRecord record);
    Task UpdateFormRecordAsync(PdfFormRecord record);
    Task AddFormChangeAsync(PdfFormChange change);
    Task<List<PdfFormChange>> GetRecentChangesAsync(int count = 10);
}
