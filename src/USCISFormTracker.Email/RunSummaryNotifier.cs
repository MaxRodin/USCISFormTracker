using Microsoft.Extensions.Logging;
using USCISFormTracker.Core.Models;
using USCISFormTracker.Formatting;

namespace USCISFormTracker.Email;

public class RunSummaryNotifier : IRunSummaryNotifier
{
    private readonly IEmailSender _emailSender;
    private readonly IRunSummaryFormatter _formatter;
    private readonly MailgunOptions _options;
    private readonly ILogger<RunSummaryNotifier> _logger;

    public RunSummaryNotifier(
        IEmailSender emailSender,
        IRunSummaryFormatter formatter,
        MailgunOptions options,
        ILogger<RunSummaryNotifier> logger)
    {
        _emailSender = emailSender;
        _formatter = formatter;
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(FormRunSummary summary)
    {
        if (!summary.HasChanges)
        {
            _logger.LogInformation("Run summary contains no changes; no email sent");
            return;
        }

        _logger.LogInformation(
            "Sending run summary: {NewCount} new, {ChangedCount} changed, {DeletedCount} deleted",
            summary.AddedForms.Count,
            summary.ChangedForms.Count,
            summary.DeletedForms.Count);

        var message = new EmailMessage
        {
            To = _options.MailingListAddress,
            Subject = BuildSubject(summary),
            HtmlBody = _formatter.FormatAsHtml(summary),
            TextBody = _formatter.FormatAsText(summary)
        };

        await _emailSender.SendEmailAsync(message);
        _logger.LogInformation("Run summary email sent to mailing list");
    }

    public static string BuildSubject(FormRunSummary summary) =>
        $"USCIS Form Tracker - Daily Summary ({summary.AddedForms.Count} new, {summary.ChangedForms.Count} changed, {summary.DeletedForms.Count} deleted)";
}
