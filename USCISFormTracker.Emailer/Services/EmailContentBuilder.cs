using USCISFormTracker.Dto;
using USCISFormTracker.Formatting;

namespace USCISFormTracker.Emailer.Services;

public class EmailContentBuilder : IEmailContentBuilder
{
    private readonly IRunSummaryFormatter _formatter;

    public EmailContentBuilder(IRunSummaryFormatter formatter)
    {
        _formatter = formatter;
    }

    public (string subject, string htmlBody, string textBody) BuildRunSummaryEmail(RunSummaryMessage summary)
    {
        var subject = $"USCIS Form Tracker - Daily Summary ({summary.NewFormsCount} new, {summary.ChangedFormsCount} changed, {summary.DeletedFormsCount} deleted)";

        var htmlBody = _formatter.FormatAsHtml(summary);
        var textBody = _formatter.FormatAsText(summary);

        return (subject, htmlBody, textBody);
    }
}
