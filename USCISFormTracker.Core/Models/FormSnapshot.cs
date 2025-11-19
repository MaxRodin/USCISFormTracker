namespace USCISFormTracker.Core.Models;

/// <summary>
/// Snapshot of a form's current state - passed to Core for comparison
/// </summary>
public class FormSnapshot
{
    public required string FileName { get; set; }
    public required string FullLink { get; set; }
    public required string FormName { get; set; }
    public required string Hash { get; set; }
    public required string ExtractedText { get; set; }
    public string? LatestPdfPath { get; set; } // Path to most recent PDF file
}
