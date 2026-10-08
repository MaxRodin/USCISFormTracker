using USCISFormTracker.Dto;

namespace USCISFormTracker.Emailer.Services;

public interface IEmailContentBuilder
{
    (string subject, string htmlBody, string textBody) BuildRunSummaryEmail(RunSummaryMessage summary);
}
