using USCISFormTracker.Emailer.Models;

namespace USCISFormTracker.Emailer;

public interface IEmailSender
{
    Task SendEmailAsync(EmailMessage message);
}
