namespace USCISFormTracker.Core.Models;

public class PdfLinkInfo
{
    public required string FileName { get; set; } // e.g., "i-751.pdf"
    public required string FullLink { get; set; } // e.g., "https://www.uscis.gov/sites/default/files/document/forms/i-751.pdf"
}
