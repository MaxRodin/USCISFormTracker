using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core.Data;

public interface IFormRepository
{
    Task<PdfFormRecord?> GetFormRecordByLinkAsync(string link);
    Task<List<PdfFormRecord>> GetAllFormRecordsAsync();
    Task AddFormRecordAsync(PdfFormRecord record);
    Task UpdateFormRecordAsync(PdfFormRecord record);
    Task AddFormChangeAsync(PdfFormChange change);
    Task<List<PdfFormChange>> GetRecentChangesAsync(int count = 10);
}
