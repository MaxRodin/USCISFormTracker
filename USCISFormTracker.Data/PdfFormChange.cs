namespace USCISFormTracker.Data;

public class PdfFormChange
{
    public int Id { get; set; }
    public required string FileName { get; set; } // Filename only, e.g., "i-751.pdf"
    public required string FullLink { get; set; } // Complete URL at time of change detection
    public required string FormName { get; set; }
    public required string OldHash { get; set; }
    public required string NewHash { get; set; }
    public required string DiffLinesSerialized { get; set; } // JSON serialized DiffLines
    public required DateTime DetectedChangeTime { get; set; }
}
