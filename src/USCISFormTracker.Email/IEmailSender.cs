namespace USCISFormTracker.Email;

public interface IEmailSender
{
    Task SendEmailAsync(EmailMessage message);

    /// <summary>
    /// Adds (or re-subscribes) an address to the configured Mailgun mailing list.
    /// </summary>
    Task AddToMailingListAsync(string email);
}
