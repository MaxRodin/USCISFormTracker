namespace USCISFormTracker.Dto;

/// <summary>
/// Message published when a PDF form change is detected
/// </summary>
public class FormChangeDetectedMessage
{
    public required string FileName { get; set; }
    public required string FullLink { get; set; }
    public required string FormName { get; set; }
    public required string OldHash { get; set; }
    public required string NewHash { get; set; }
    public required DateTime DetectedChangeTime { get; set; }

    // DiffLines data
    public required List<string> AddedLines { get; set; }
    public required List<string> DeletedLines { get; set; }
    public required List<string> ModifiedLines { get; set; }
}
