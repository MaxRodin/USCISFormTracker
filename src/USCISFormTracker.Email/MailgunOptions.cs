namespace USCISFormTracker.Email;

/// <summary>
/// Mailgun account settings. Populated from MAILGUN_* configuration keys by
/// <see cref="ServiceExtensions.AddEmailServices"/>.
/// </summary>
public class MailgunOptions
{
    public required string ApiKey { get; init; }
    public required string Domain { get; init; }
    public required string FromEmail { get; init; }
    public required string FromName { get; init; }
    public required string MailingListAddress { get; init; }
}
