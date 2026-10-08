namespace USCISFormTracker.Core.Models;

/// <summary>
/// Represents a PDF form scraped from the USCIS website
/// </summary>
public class ScrapedPdf
{
    public required string FileName { get; set; } // e.g., "i-751.pdf"
    public required string FullLink { get; set; } // e.g., "https://www.uscis.gov/sites/default/files/document/forms/i-751.pdf"
}
