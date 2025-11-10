namespace USCISFormTracker.Dto;

/// <summary>
/// Message published when a new PDF form is discovered
/// </summary>
public class FormAddedMessage
{
    public required string FileName { get; set; }
    public required string FullLink { get; set; }
    public required string FormName { get; set; }
    public required string Hash { get; set; }
    public required DateTime DiscoveredTime { get; set; }
}
