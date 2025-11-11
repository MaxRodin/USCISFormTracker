namespace USCISFormTracker.Dto;

/// <summary>
/// Message published when a user subscribes to the mailing list
/// </summary>
public class AddToMailingListMessage
{
    public required string Email { get; set; }
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
}
