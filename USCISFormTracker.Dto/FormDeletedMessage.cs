namespace USCISFormTracker.Dto;

/// <summary>
/// Message published when a PDF form is no longer available
/// </summary>
public class FormDeletedMessage
{
    public required string Link { get; set; }
    public required string FormName { get; set; }
    public required DateTime LastSeen { get; set; }
}
