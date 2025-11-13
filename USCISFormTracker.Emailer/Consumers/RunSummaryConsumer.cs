using System.Text;
using MassTransit;
using USCISFormTracker.Dto;
using USCISFormTracker.Emailer.Models;

namespace USCISFormTracker.Emailer.Consumers;

public class RunSummaryConsumer : IConsumer<RunSummaryMessage>
{
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RunSummaryConsumer> _logger;

    public RunSummaryConsumer(
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<RunSummaryConsumer> logger)
    {
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RunSummaryMessage> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Processing run summary: {NewCount} new, {ChangedCount} changed, {DeletedCount} deleted",
            message.NewFormsCount,
            message.ChangedFormsCount,
            message.DeletedFormsCount);

        var mailingListAddress = _configuration["Mailgun:MailingListAddress"]
            ?? throw new InvalidOperationException("Mailgun:MailingListAddress not configured");

        string subject;
        if (message.IsFirstRun)
        {
            subject = $"USCIS Form Tracker - Initial Sync Complete ({message.NewFormsCount} forms added)";
        }
        else
        {
            subject = $"USCIS Form Tracker - Run Summary ({message.NewFormsCount} new, {message.ChangedFormsCount} changed)";
        }

        var htmlBody = BuildSummaryEmailHtml(message);
        var textBody = BuildSummaryEmailText(message);

        var emailMessage = new EmailMessage
        {
            To = mailingListAddress,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody
        };

        await _emailSender.SendEmailAsync(emailMessage);
        _logger.LogInformation("Run summary email sent to mailing list");
    }

    private string BuildSummaryEmailHtml(RunSummaryMessage summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><body>");

        if (summary.IsFirstRun)
        {
            sb.AppendLine("<h2>USCIS Form Tracker - Initial Sync Complete</h2>");
            sb.AppendLine($"<p>The USCIS Form Tracker has completed its first run and is now monitoring <strong>{summary.TotalFormsOnWebsite} forms</strong>.</p>");
        }
        else
        {
            sb.AppendLine("<h2>USCIS Form Tracker - Run Summary</h2>");
        }

        sb.AppendLine($"<p><strong>Run Time:</strong> {summary.RunTime:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("<hr>");

        // Summary stats
        sb.AppendLine("<h3>Summary</h3>");
        sb.AppendLine("<ul>");
        sb.AppendLine($"<li><strong>{summary.TotalFormsOnWebsite}</strong> total forms on USCIS website</li>");
        sb.AppendLine($"<li><strong style=\"color: green;\">{summary.NewFormsCount}</strong> new forms discovered</li>");
        sb.AppendLine($"<li><strong style=\"color: orange;\">{summary.ChangedFormsCount}</strong> forms changed</li>");
        sb.AppendLine($"<li><strong style=\"color: red;\">{summary.DeletedFormsCount}</strong> forms removed</li>");
        sb.AppendLine("</ul>");

        // New forms section
        if (summary.NewForms.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: green;\">New Forms Discovered</h3>");

            var displayCount = Math.Min(summary.NewForms.Count, 50);
            sb.AppendLine("<ul>");
            foreach (var form in summary.NewForms.Take(displayCount))
            {
                sb.AppendLine($"<li><strong>{System.Net.WebUtility.HtmlEncode(form.FormName)}</strong> - <a href=\"{System.Net.WebUtility.HtmlEncode(form.FullLink)}\">{System.Net.WebUtility.HtmlEncode(form.FileName)}</a></li>");
            }
            sb.AppendLine("</ul>");

            if (summary.NewForms.Count > displayCount)
            {
                sb.AppendLine($"<p><em>... and {summary.NewForms.Count - displayCount} more forms</em></p>");
            }
        }

        // Changed forms section
        if (summary.ChangedForms.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: orange;\">Changed Forms</h3>");
            sb.AppendLine("<ul>");
            foreach (var form in summary.ChangedForms.Take(20))
            {
                sb.AppendLine($"<li><strong>{System.Net.WebUtility.HtmlEncode(form.FormName)}</strong> - <a href=\"{System.Net.WebUtility.HtmlEncode(form.FullLink)}\">{System.Net.WebUtility.HtmlEncode(form.FileName)}</a></li>");
            }
            sb.AppendLine("</ul>");

            if (summary.ChangedForms.Count > 20)
            {
                sb.AppendLine($"<p><em>... and {summary.ChangedForms.Count - 20} more</em></p>");
            }
        }

        // Deleted forms section
        if (summary.DeletedForms.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: red;\">Removed Forms</h3>");
            sb.AppendLine("<ul>");
            foreach (var form in summary.DeletedForms)
            {
                sb.AppendLine($"<li><strong>{System.Net.WebUtility.HtmlEncode(form.FormName)}</strong> ({System.Net.WebUtility.HtmlEncode(form.FileName)})</li>");
            }
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string BuildSummaryEmailText(RunSummaryMessage summary)
    {
        var sb = new StringBuilder();

        if (summary.IsFirstRun)
        {
            sb.AppendLine("USCIS Form Tracker - Initial Sync Complete");
            sb.AppendLine("===========================================");
            sb.AppendLine();
            sb.AppendLine($"The USCIS Form Tracker has completed its first run and is now monitoring {summary.TotalFormsOnWebsite} forms.");
        }
        else
        {
            sb.AppendLine("USCIS Form Tracker - Run Summary");
            sb.AppendLine("=================================");
        }

        sb.AppendLine();
        sb.AppendLine($"Run Time: {summary.RunTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Summary stats
        sb.AppendLine("Summary:");
        sb.AppendLine($"  Total forms on website: {summary.TotalFormsOnWebsite}");
        sb.AppendLine($"  New forms: {summary.NewFormsCount}");
        sb.AppendLine($"  Changed forms: {summary.ChangedFormsCount}");
        sb.AppendLine($"  Removed forms: {summary.DeletedFormsCount}");
        sb.AppendLine();

        // New forms
        if (summary.NewForms.Count > 0)
        {
            sb.AppendLine("=== New Forms Discovered ===");
            var displayCount = Math.Min(summary.NewForms.Count, 50);
            foreach (var form in summary.NewForms.Take(displayCount))
            {
                sb.AppendLine($"  • {form.FormName} ({form.FileName})");
                sb.AppendLine($"    {form.FullLink}");
            }
            if (summary.NewForms.Count > displayCount)
            {
                sb.AppendLine($"  ... and {summary.NewForms.Count - displayCount} more forms");
            }
            sb.AppendLine();
        }

        // Changed forms
        if (summary.ChangedForms.Count > 0)
        {
            sb.AppendLine("=== Changed Forms ===");
            foreach (var form in summary.ChangedForms.Take(20))
            {
                sb.AppendLine($"  • {form.FormName} ({form.FileName})");
                sb.AppendLine($"    {form.FullLink}");
            }
            if (summary.ChangedForms.Count > 20)
            {
                sb.AppendLine($"  ... and {summary.ChangedForms.Count - 20} more");
            }
            sb.AppendLine();
        }

        // Deleted forms
        if (summary.DeletedForms.Count > 0)
        {
            sb.AppendLine("=== Removed Forms ===");
            foreach (var form in summary.DeletedForms)
            {
                sb.AppendLine($"  • {form.FormName} ({form.FileName})");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
