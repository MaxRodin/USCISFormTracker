namespace USCISFormTracker.Core.Models;

public class PdfFormChange
{
    public int Id { get; set; }
    public required string Link { get; set; }
    public required string FormName { get; set; }
    public required string OldHash { get; set; }
    public required string NewHash { get; set; }
    public required string DiffLinesSerialized { get; set; } // JSON serialized DiffLines
    public required DateTime DetectedChangeTime { get; set; }
}
