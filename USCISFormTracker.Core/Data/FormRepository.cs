using Microsoft.EntityFrameworkCore;
using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core.Data;

public class FormRepository : IFormRepository
{
    private readonly FormTrackerDbContext _context;

    public FormRepository(FormTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<PdfFormRecord?> GetFormRecordByLinkAsync(string link)
    {
        return await _context.FormRecords
            .FirstOrDefaultAsync(f => f.Link == link);
    }

    public async Task<List<PdfFormRecord>> GetAllFormRecordsAsync()
    {
        return await _context.FormRecords.ToListAsync();
    }

    public async Task AddFormRecordAsync(PdfFormRecord record)
    {
        _context.FormRecords.Add(record);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateFormRecordAsync(PdfFormRecord record)
    {
        _context.FormRecords.Update(record);
        await _context.SaveChangesAsync();
    }

    public async Task AddFormChangeAsync(PdfFormChange change)
    {
        _context.FormChanges.Add(change);
        await _context.SaveChangesAsync();
    }

    public async Task<List<PdfFormChange>> GetRecentChangesAsync(int count = 10)
    {
        return await _context.FormChanges
            .OrderByDescending(c => c.DetectedChangeTime)
            .Take(count)
            .ToListAsync();
    }
}
