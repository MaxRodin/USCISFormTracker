using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public interface IEmailSender
{
    Task SendEmailAsync(EmailMessage message);
}
