namespace USCISFormTracker.Processor.Models;

public class PdfFormRecord
{
    public int Id { get; set; }
    public required string FileName { get; set; } // Filename only, e.g., "i-751.pdf"
    public required string FullLink { get; set; } // Complete URL, e.g., "https://www.uscis.gov/sites/default/files/document/forms/i-751.pdf"
    public required string FormName { get; set; }
    public required string Hash { get; set; }
    public required string ExtractedText { get; set; } // Full text content extracted from PDF for diffing
    public DateTime LastChecked { get; set; }
}
